using System.Text.Json;
using FCG.Application.Purchases;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CatalogAPI.Messaging;

public sealed class RabbitMqOrderPublisher(IOptions<RabbitMqOptions> options) : IOrderEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task PublishAsync(OrderPlacedEvent value, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var current = await GetChannelAsync(ct);
            var body = JsonSerializer.SerializeToUtf8Bytes(value);
            await current.BasicPublishAsync(options.Value.Exchange, "order.placed", mandatory: true,
                basicProperties: new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent }, body: body,
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
        channel = await connection.CreateChannelAsync(cancellationToken: ct);
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
