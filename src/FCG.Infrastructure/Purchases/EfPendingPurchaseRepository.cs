using FCG.Application.Purchases;
using FCG.Domain.Purchases;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Purchases;

public sealed class EfPendingPurchaseRepository(AppDbContext db) : IPendingPurchaseRepository
{
    public Task<PendingPurchase?> FindAsync(Guid userId, Guid gameId, CancellationToken ct) =>
        db.PendingPurchases.SingleOrDefaultAsync(x => x.UserId == userId && x.GameId == gameId, ct);
    public Task AddAsync(PendingPurchase value, CancellationToken ct) => db.PendingPurchases.AddAsync(value, ct).AsTask();
    public void Remove(PendingPurchase value) => db.PendingPurchases.Remove(value);
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
