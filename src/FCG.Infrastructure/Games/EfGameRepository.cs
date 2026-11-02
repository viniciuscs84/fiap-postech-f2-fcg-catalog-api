using FCG.Application.Games;
using FCG.Domain.Games;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Games;

/// <summary>EF Core repository for catalog games.</summary>
public sealed class EfGameRepository(AppDbContext dbContext) : IGameRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Game>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Games
            .AsNoTracking()
            .OrderBy(game => game.Title)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Game?> FindByIdAsync(Guid gameId, CancellationToken cancellationToken)
    {
        return dbContext.Games
            .AsNoTracking()
            .SingleOrDefaultAsync(game => game.Id == gameId, cancellationToken);
    }

    public Task<Game?> FindTrackedByIdAsync(Guid gameId, CancellationToken cancellationToken) =>
        dbContext.Games.SingleOrDefaultAsync(game => game.Id == gameId, cancellationToken);

    public Task<bool> HasAcquisitionsAsync(Guid gameId, CancellationToken cancellationToken) =>
        dbContext.AcquiredGames.AsNoTracking().AnyAsync(item => item.GameId == gameId, cancellationToken);

    /// <inheritdoc />
    public Task AddAsync(Game game, CancellationToken cancellationToken)
    {
        return dbContext.Games.AddAsync(game, cancellationToken).AsTask();
    }

    public void Remove(Game game) => dbContext.Games.Remove(game);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
