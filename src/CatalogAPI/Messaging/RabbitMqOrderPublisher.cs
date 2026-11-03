using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CatalogAPI.Messaging;

public sealed class RabbitMqOrderPublisher(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task PublishAsync(Guid eventId, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var current = await GetChannelAsync(ct);
            await current.BasicPublishAsync(options.Value.Exchange, "order.placed", mandatory: true,
                basicProperties: new BasicProperties
                {
                    MessageId = eventId.ToString(), ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent, Type = "OrderPlacedEvent",
                    Headers = new Dictionary<string, object?> { ["x-fcg-contract-version"] = 2 }
                }, body: body,
                cancellationToken: ct);
        }
        finally { gate.Release(); }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (channel is { IsOpen: true }) return channel;
        if (connection is not { IsOpen: true })
        {
            var value = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = value.HostName, Port = value.Port, UserName = value.UserName,
                Password = value.Password, AutomaticRecoveryEnabled = true
            };
            connection = await factory.CreateConnectionAsync(ct);
        }
        channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync("payments.order-placed", durable: true, exclusive: false,
            autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync("payments.order-placed", options.Value.Exchange, "order.placed", cancellationToken: ct);
        return channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) await channel.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        gate.Dispose();
    }
}
