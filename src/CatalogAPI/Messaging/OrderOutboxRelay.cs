using System.Text;
using FCG.Application.Purchases;

namespace CatalogAPI.Messaging;

public sealed class OrderOutboxRelay(
    IServiceScopeFactory scopes,
    RabbitMqOrderPublisher publisher,
    EventMetrics metrics,
    ILogger<OrderOutboxRelay> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ConfirmedRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan ReceiptRetention = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var found = await PublishBatchAsync(stoppingToken);
                if (!found) await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Order outbox relay failed; pending events remain stored for retry.");
                await Task.Delay(FailureDelay, stoppingToken);
            }
        }
    }

    internal async Task<bool> PublishBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOrderOutboxRepository>();
        var pending = await outbox.FindPendingAsync(20, ct);
        foreach (var message in pending)
        {
            message.RecordAttempt(DateTime.UtcNow);
            await outbox.SaveChangesAsync(ct);
            try { await publisher.PublishAsync(message.EventId, Encoding.UTF8.GetBytes(message.Payload), ct); }
            catch
            {
                metrics.RecordPublishFailure();
                throw;
            }
            message.MarkPublished(DateTime.UtcNow);
            await outbox.SaveChangesAsync(ct);
            logger.LogInformation("Published OrderPlacedEvent {EventId} for order {OrderId}.", message.EventId, message.OrderId);
        }
        await outbox.DeleteConfirmedBeforeAsync(DateTime.UtcNow.Subtract(ConfirmedRetention), ct);
        var purchases = scope.ServiceProvider.GetRequiredService<IPendingPurchaseRepository>();
        await purchases.DeleteProcessedBeforeAsync(DateTime.UtcNow.Subtract(ReceiptRetention), ct);
        return pending.Count > 0;
    }
}
