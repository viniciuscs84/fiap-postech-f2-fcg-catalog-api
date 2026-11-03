using System.Text.Json;
using FCG.Application.Purchases;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CatalogAPI.Messaging;

public sealed class PaymentResultConsumer(
    IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options,
    EventMetrics metrics,
    ILogger<PaymentResultConsumer> logger) : BackgroundService
{
    private const string QueueName = "catalog.payment-processed";
    private const string RoutingKey = "payment.processed";
    private const string DeadQueueName = "catalog.payment-processed.dead";
    private const string DeadRoutingKey = "payment.processed.catalog.dead";
    private const string RetryHeader = "x-fcg-attempt";
    private const int MaximumDeliveryAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var value = options.Value;
                var factory = new ConnectionFactory
                {
                    HostName = value.HostName, Port = value.Port, UserName = value.UserName,
                    Password = value.Password, AutomaticRecoveryEnabled = true
                };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true), stoppingToken);
                await channel.ExchangeDeclareAsync(value.Exchange, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false,
                    autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await channel.QueueBindAsync(QueueName, value.Exchange, RoutingKey, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(DeadQueueName, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: stoppingToken);
                await channel.QueueBindAsync(DeadQueueName, value.Exchange, DeadRoutingKey,
                    cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var payment = JsonSerializer.Deserialize<PaymentProcessedEvent>(delivery.Body.Span);
                        if (payment is null) throw new JsonException("Payment result is empty.");
                        await using var scope = scopes.CreateAsyncScope();
                        var result = await scope.ServiceProvider.GetRequiredService<PaymentResultService>()
                            .HandleAsync(payment, stoppingToken);
                        if (result == PaymentApplicationKind.InvalidStatus)
                            throw new ArgumentException("Payment result has invalid identity, version, or status.");
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        metrics.RecordConsumeFailure();
                        var permanent = exception is JsonException or ArgumentException;
                        var attempt = permanent ? MaximumDeliveryAttempts : ReadAttempt(delivery.BasicProperties.Headers) + 1;
                        var destination = attempt >= MaximumDeliveryAttempts ? DeadRoutingKey : RoutingKey;
                        logger.LogWarning(exception,
                            "Payment result {EventId} for order {OrderId} failed on attempt {Attempt}; routing to {Destination}.",
                            delivery.BasicProperties.MessageId, delivery.BasicProperties.CorrelationId,
                            attempt, destination);
                        try
                        {
                            var headers = delivery.BasicProperties.Headers is null
                                ? new Dictionary<string, object?>()
                                : new Dictionary<string, object?>(delivery.BasicProperties.Headers);
                            headers[RetryHeader] = attempt;
                            var properties = new BasicProperties
                            {
                                ContentType = delivery.BasicProperties.ContentType ?? "application/json",
                                DeliveryMode = DeliveryModes.Persistent,
                                Type = delivery.BasicProperties.Type ?? nameof(PaymentProcessedEvent),
                                MessageId = delivery.BasicProperties.MessageId,
                                CorrelationId = delivery.BasicProperties.CorrelationId,
                                Headers = headers
                            };
                            await channel.BasicPublishAsync(value.Exchange, destination, mandatory: true,
                                basicProperties: properties, body: delivery.Body,
                                cancellationToken: stoppingToken);
                            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        }
                        catch (Exception publishException)
                        {
                            logger.LogError(publishException,
                                "Could not retain failed payment result; requeueing original delivery.");
                            await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                        }
                    }
                };
                await channel.BasicConsumeAsync(QueueName, false, consumer, stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "RabbitMQ payment consumer unavailable; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static int ReadAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryHeader, out var value) || value is null)
            return 0;
        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => checked((int)number),
            _ => 0
        };
    }
}
