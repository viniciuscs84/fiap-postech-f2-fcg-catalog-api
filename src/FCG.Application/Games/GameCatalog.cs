namespace FCG.Application.Games;

/// <summary>Represents a game returned by catalog queries.</summary>
public sealed record GameCatalogItemResponse(
    Guid Id,
    string Title,
    string Description,
    string Genre,
    decimal Price,
    DateTime CreatedAtUtc);

/// <summary>Query use cases for the game catalog.</summary>
public interface IGameCatalogService
{
    /// <summary>Lists all games available in the catalog.</summary>
    Task<IReadOnlyList<GameCatalogItemResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets a catalog game by identifier.</summary>
    Task<GameCatalogItemResponse?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken);
}

/// <summary>Coordinates read operations over the game catalog.</summary>
public sealed class GameCatalogService(IGameRepository repository) : IGameCatalogService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GameCatalogItemResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var games = await repository.ListAsync(cancellationToken);
        return games.Select(game => new GameCatalogItemResponse(
            game.Id,
            game.Title,
            game.Description,
            game.Genre,
            game.Price,
            game.CreatedAtUtc)).ToArray();
    }

    /// <inheritdoc />
    public async Task<GameCatalogItemResponse?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await repository.FindByIdAsync(gameId, cancellationToken);
        return game is null
            ? null
            : new GameCatalogItemResponse(
                game.Id,
                game.Title,
                game.Description,
                game.Genre,
                game.Price,
                game.CreatedAtUtc);
    }
}
