using FCG.Domain.Games;

namespace FCG.Application.Games;

/// <summary>Persistence boundary for catalog games.</summary>
public interface IGameRepository
{
    /// <summary>Lists all games in the catalog.</summary>
    Task<IReadOnlyList<Game>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Finds a game by its identifier.</summary>
    Task<Game?> FindByIdAsync(Guid gameId, CancellationToken cancellationToken);

    Task<Game?> FindTrackedByIdAsync(Guid gameId, CancellationToken cancellationToken);

    Task<bool> HasAcquisitionsAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>Adds a game to the current persistence unit of work.</summary>
    Task AddAsync(Game game, CancellationToken cancellationToken);

    void Remove(Game game);

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
