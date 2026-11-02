namespace FCG.Application.Games;

/// <summary>Coordinates game registration.</summary>
public interface IGameRegistrationService
{
    /// <summary>Validates and registers a game.</summary>
    /// <param name="command">Game data.</param>
    /// <param name="createdByUserId">Administrator creating the game.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The registration outcome.</returns>
    Task<GameRegistrationOutcome> RegisterAsync(CreateGameCommand command, Guid createdByUserId, CancellationToken cancellationToken);
}
