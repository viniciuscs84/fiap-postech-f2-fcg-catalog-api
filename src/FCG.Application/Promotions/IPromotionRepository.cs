using FCG.Domain.Promotions;

namespace FCG.Application.Promotions;

/// <summary>Persistence boundary for promotions.</summary>
public interface IPromotionRepository
{
    /// <summary>Finds a promotion by its normalized code.</summary>
    Task<Promotion?> FindByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken);

    /// <summary>Adds a promotion to the current persistence unit of work.</summary>
    Task AddAsync(Promotion promotion, CancellationToken cancellationToken);

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
