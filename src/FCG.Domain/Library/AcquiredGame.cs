namespace FCG.Domain.Library;

/// <summary>Represents the acquisition of a game by a user.</summary>
public sealed class AcquiredGame
{
    private AcquiredGame()
    {
    }

    /// <summary>Gets the acquisition identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the acquiring user identifier.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the acquired game identifier.</summary>
    public Guid GameId { get; private set; }

    /// <summary>Gets the acquisition timestamp in UTC.</summary>
    public DateTime AcquiredAtUtc { get; private set; }

    /// <summary>Gets the associated game.</summary>
    public Games.Game Game { get; private set; } = null!;

    /// <summary>Creates a game acquisition.</summary>
    /// <param name="userId">Acquiring user identifier.</param>
    /// <param name="gameId">Acquired game identifier.</param>
    /// <param name="acquiredAtUtc">Acquisition timestamp.</param>
    /// <returns>A new acquisition.</returns>
    public static AcquiredGame Acquire(Guid userId, Guid gameId, DateTime acquiredAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", nameof(userId));
        }

        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("A game id is required.", nameof(gameId));
        }

        return new AcquiredGame
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            AcquiredAtUtc = DateTime.SpecifyKind(acquiredAtUtc, DateTimeKind.Utc)
        };
    }
}
