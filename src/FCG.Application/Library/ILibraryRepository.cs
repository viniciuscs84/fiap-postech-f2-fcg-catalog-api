using FCG.Domain.Library;

namespace FCG.Application.Library;

/// <summary>Persistence boundary for acquired games.</summary>
public interface ILibraryRepository
{
    /// <summary>Finds all acquired games for a user.</summary>
    Task<IReadOnlyList<LibraryItemResponse>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Determines whether a game is already associated with a user's library.</summary>
    Task<bool> ExistsAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);

    /// <summary>Adds a game acquisition to the current persistence unit of work.</summary>
    Task AddAsync(AcquiredGame acquisition, CancellationToken cancellationToken);

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
