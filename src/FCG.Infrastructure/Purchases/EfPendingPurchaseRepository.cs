using FCG.Application.Purchases;
using FCG.Domain.Purchases;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Purchases;

public sealed class EfPendingPurchaseRepository(AppDbContext db) : IPendingPurchaseRepository
{
    public Task<PendingPurchase?> FindAsync(Guid userId, Guid gameId, CancellationToken ct) =>
        db.PendingPurchases.SingleOrDefaultAsync(x => x.UserId == userId && x.GameId == gameId, ct);
    public Task<PendingPurchase?> FindByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        db.PendingPurchases.SingleOrDefaultAsync(x => x.Id == orderId, ct);
    public Task AddAsync(PendingPurchase value, CancellationToken ct) => db.PendingPurchases.AddAsync(value, ct).AsTask();
    public Task AddOutboxAsync(OrderOutboxMessage value, CancellationToken ct) => db.OrderOutbox.AddAsync(value, ct).AsTask();
    public Task<bool> WasProcessedAsync(Guid eventId, Guid orderId, CancellationToken ct) =>
        db.ProcessedPaymentEvents.AnyAsync(x => x.EventId == eventId || x.OrderId == orderId, ct);
    public Task AddProcessedAsync(ProcessedPaymentEvent value, CancellationToken ct) =>
        db.ProcessedPaymentEvents.AddAsync(value, ct).AsTask();
    public Task<int> DeleteProcessedBeforeAsync(DateTime cutoffUtc, CancellationToken ct) =>
        db.ProcessedPaymentEvents.Where(x => x.ProcessedAtUtc < cutoffUtc).ExecuteDeleteAsync(ct);
    public void Remove(PendingPurchase value) => db.PendingPurchases.Remove(value);
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
