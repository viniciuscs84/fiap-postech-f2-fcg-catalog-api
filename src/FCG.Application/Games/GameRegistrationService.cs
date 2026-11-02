using FCG.Domain.Games;

namespace FCG.Application.Games;

/// <summary>Implements the game registration use case.</summary>
public sealed class GameRegistrationService(IGameRepository repository) : IGameRegistrationService
{
    /// <inheritdoc />
    public async Task<GameRegistrationOutcome> RegisterAsync(CreateGameCommand command, Guid createdByUserId, CancellationToken cancellationToken)
    {
        var validationErrors = GameRegistrationRules.Validate(command);
        if (validationErrors.Count > 0)
        {
            return new GameRegistrationOutcome.ValidationFailure(validationErrors);
        }

        var game = Game.Create(
            GameRegistrationRules.Normalize(command.Title),
            GameRegistrationRules.Normalize(command.Description),
            GameRegistrationRules.Normalize(command.Genre),
            createdByUserId,
            DateTime.UtcNow,
            command.Price);

        await repository.AddAsync(game, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new GameRegistrationOutcome.Success(new RegisteredGameResponse(
            game.Id,
            game.Title,
            game.Description,
            game.Genre,
            game.Price,
            game.CreatedByUserId,
            game.CreatedAtUtc));
    }
}
