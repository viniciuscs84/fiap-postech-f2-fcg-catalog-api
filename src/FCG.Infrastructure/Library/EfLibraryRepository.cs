using FCG.Application.Library;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Library;

/// <summary>EF Core repository for acquired game libraries.</summary>
public sealed class EfLibraryRepository(AppDbContext dbContext) : ILibraryRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LibraryItemResponse>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.AcquiredGames
            .AsNoTracking()
            .Where(acquisition => acquisition.UserId == userId)
            .OrderBy(acquisition => acquisition.AcquiredAtUtc)
            .Select(acquisition => new LibraryItemResponse(
                acquisition.GameId,
                acquisition.Game.Title,
                acquisition.Game.Description,
                acquisition.Game.Genre,
                acquisition.AcquiredAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        return dbContext.AcquiredGames
            .AsNoTracking()
            .AnyAsync(acquisition => acquisition.UserId == userId && acquisition.GameId == gameId, cancellationToken);
    }

    /// <inheritdoc />
    public Task AddAsync(AcquiredGame acquisition, CancellationToken cancellationToken)
    {
        return dbContext.AcquiredGames.AddAsync(acquisition, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
