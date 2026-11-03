using FCG.Application.Purchases;
using FCG.Domain.Purchases;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Purchases;

public sealed class EfOrderOutboxRepository(AppDbContext db) : IOrderOutboxRepository
{
    public async Task<IReadOnlyList<OrderOutboxMessage>> FindPendingAsync(int limit, CancellationToken ct) =>
        await db.OrderOutbox.Where(x => x.PublishedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc).Take(limit).ToListAsync(ct);

    public Task<OrderOutboxMessage?> FindReplayEligibleAsync(Guid eventId, DateTime confirmedAfterUtc, CancellationToken ct) =>
        db.OrderOutbox.SingleOrDefaultAsync(x =>
            x.EventId == eventId && x.PublishedAtUtc != null && x.PublishedAtUtc >= confirmedAfterUtc, ct);

    public Task<int> DeleteConfirmedBeforeAsync(DateTime cutoffUtc, CancellationToken ct) =>
        db.OrderOutbox.Where(x => x.PublishedAtUtc != null && x.PublishedAtUtc < cutoffUtc).ExecuteDeleteAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
