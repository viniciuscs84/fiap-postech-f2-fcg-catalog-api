using FCG.Application.Promotions;
using FCG.Domain.Promotions;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Promotions;

/// <summary>EF Core repository for promotions.</summary>
public sealed class EfPromotionRepository(AppDbContext dbContext) : IPromotionRepository
{
    /// <inheritdoc />
    public Task<Promotion?> FindByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken)
    {
        return dbContext.Promotions.SingleOrDefaultAsync(promotion => promotion.NormalizedCode == normalizedCode, cancellationToken);
    }

    /// <inheritdoc />
    public Task AddAsync(Promotion promotion, CancellationToken cancellationToken)
    {
        return dbContext.Promotions.AddAsync(promotion, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
