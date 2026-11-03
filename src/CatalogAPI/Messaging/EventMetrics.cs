using System.Text.Json;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Messaging;

public sealed class EventMetrics
{
    private long publishFailures;
    private long consumeFailures;
    public long PublishFailures => Interlocked.Read(ref publishFailures);
    public long ConsumeFailures => Interlocked.Read(ref consumeFailures);
    public void RecordPublishFailure() => Interlocked.Increment(ref publishFailures);
    public void RecordConsumeFailure() => Interlocked.Increment(ref consumeFailures);
}

public sealed class EventMetricsWorker(
    IServiceScopeFactory scopes, EventMetrics metrics, TimeProvider timeProvider,
    ILogger<EventMetricsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pending = db.OrderOutbox.Where(value => value.PublishedAtUtc == null);
                var count = await pending.CountAsync(stoppingToken);
                var oldest = await pending.Select(value => (DateTime?)value.CreatedAtUtc)
                    .MinAsync(stoppingToken);
                var age = oldest is null ? 0 : Math.Max(0,
                    (timeProvider.GetUtcNow().UtcDateTime - oldest.Value).TotalSeconds);
                var snapshot = JsonSerializer.Serialize(new
                {
                    service = "CatalogAPI",
                    fcg_outbox_pending = count,
                    fcg_outbox_oldest_age_seconds = age,
                    fcg_event_publish_failures_total = metrics.PublishFailures,
                    fcg_event_consume_failures_total = metrics.ConsumeFailures
                });
                var path = Path.Combine(Path.GetTempPath(), "fcg-metrics.json");
                await File.WriteAllTextAsync(path + ".tmp", snapshot, stoppingToken);
                File.Move(path + ".tmp", path, true);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not update internal event metrics snapshot.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
