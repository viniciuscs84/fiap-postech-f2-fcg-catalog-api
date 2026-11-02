using System.Text.Json;
using FCG.Application.Purchases;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CatalogAPI.Messaging;

public sealed class PaymentResultConsumer(
    IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options,
    ILogger<PaymentResultConsumer> logger) : BackgroundService
{
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
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.ExchangeDeclareAsync(value.Exchange, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync("catalog.payment-processed", durable: true, exclusive: false,
                    autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await channel.QueueBindAsync("catalog.payment-processed", value.Exchange, "payment.processed", cancellationToken: stoppingToken);
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
                            logger.LogWarning("Ignoring payment result with invalid status.");
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (JsonException exception)
                    {
                        logger.LogWarning(exception, "Ignoring malformed payment result.");
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Payment result processing failed.");
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync("catalog.payment-processed", false, consumer, stoppingToken);
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
}
