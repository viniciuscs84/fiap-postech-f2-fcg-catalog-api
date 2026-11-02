using FCG.Application.Games;
using FCG.Domain.Library;

namespace FCG.Application.Library;

/// <summary>Implements library queries and game acquisition associations.</summary>
public sealed class LibraryService(
    ILibraryRepository repository,
    IGameRepository gameRepository) : ILibraryService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<LibraryItemResponse>> GetMyLibraryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return repository.FindByUserIdAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GameAcquisitionOutcome> AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        var game = await gameRepository.FindByIdAsync(gameId, cancellationToken);
        if (game is null)
        {
            return new GameAcquisitionOutcome.GameNotFound();
        }

        if (await repository.ExistsAsync(userId, gameId, cancellationToken))
        {
            return new GameAcquisitionOutcome.AlreadyAcquired();
        }

        var acquiredAtUtc = DateTime.UtcNow;
        var acquisition = AcquiredGame.Acquire(userId, gameId, acquiredAtUtc);

        await repository.AddAsync(acquisition, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new GameAcquisitionOutcome.Success(new LibraryItemResponse(
            game.Id,
            game.Title,
            game.Description,
            game.Genre,
            acquisition.AcquiredAtUtc));
    }
}
