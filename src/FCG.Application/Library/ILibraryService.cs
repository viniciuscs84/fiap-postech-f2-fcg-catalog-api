namespace FCG.Application.Library;

/// <summary>Provides library use cases.</summary>
public interface ILibraryService
{
    /// <summary>Returns the authenticated user's acquired games.</summary>
    Task<IReadOnlyList<LibraryItemResponse>> GetMyLibraryAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Associates a catalog game with the authenticated user's library after an external purchase flow has completed.</summary>
    Task<GameAcquisitionOutcome> AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
}

/// <summary>Possible outcomes when associating a catalog game with a user's library.</summary>
public abstract record GameAcquisitionOutcome
{
    private GameAcquisitionOutcome()
    {
    }

    /// <summary>The game was associated with the user's library.</summary>
    public sealed record Success(LibraryItemResponse Item) : GameAcquisitionOutcome;

    /// <summary>The requested game does not exist in the catalog.</summary>
    public sealed record GameNotFound : GameAcquisitionOutcome;

    /// <summary>The game is already associated with the user's library.</summary>
    public sealed record AlreadyAcquired : GameAcquisitionOutcome;
}
