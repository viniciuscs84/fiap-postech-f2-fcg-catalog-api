namespace FCG.Application.Games;

public sealed record UpdateGameCommand(string Title, string Description, string Genre, decimal Price);

public enum GameMutationKind
{
    Success,
    NotFound,
    ValidationFailure,
    Conflict
}

public sealed record GameMutationResult(
    GameMutationKind Kind,
    GameCatalogItemResponse? Game = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IGameAdministrationService
{
    Task<GameMutationResult> UpdateAsync(Guid gameId, UpdateGameCommand command, CancellationToken cancellationToken);
    Task<GameMutationResult> DeleteAsync(Guid gameId, CancellationToken cancellationToken);
}

public sealed class GameAdministrationService(IGameRepository repository) : IGameAdministrationService
{
    public async Task<GameMutationResult> UpdateAsync(
        Guid gameId, UpdateGameCommand command, CancellationToken cancellationToken)
    {
        var errors = GameRegistrationRules.Validate(
            new CreateGameCommand(command.Title, command.Description, command.Genre, command.Price));
        if (errors.Count > 0)
            return new GameMutationResult(GameMutationKind.ValidationFailure, Errors: errors);

        var game = await repository.FindTrackedByIdAsync(gameId, cancellationToken);
        if (game is null)
            return new GameMutationResult(GameMutationKind.NotFound);

        game.Update(command.Title, command.Description, command.Genre, command.Price);
        await repository.SaveChangesAsync(cancellationToken);
        return new GameMutationResult(GameMutationKind.Success,
            new GameCatalogItemResponse(game.Id, game.Title, game.Description, game.Genre, game.Price, game.CreatedAtUtc));
    }

    public async Task<GameMutationResult> DeleteAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await repository.FindTrackedByIdAsync(gameId, cancellationToken);
        if (game is null)
            return new GameMutationResult(GameMutationKind.NotFound);
        if (await repository.HasOwnershipOrPendingPurchaseAsync(gameId, cancellationToken))
            return new GameMutationResult(GameMutationKind.Conflict);

        repository.Remove(game);
        await repository.SaveChangesAsync(cancellationToken);
        return new GameMutationResult(GameMutationKind.Success);
    }
}
