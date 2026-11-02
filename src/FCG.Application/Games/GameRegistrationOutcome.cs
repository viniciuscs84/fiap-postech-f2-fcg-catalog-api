namespace FCG.Application.Games;

/// <summary>Represents the result of game registration.</summary>
public abstract record GameRegistrationOutcome
{
    private GameRegistrationOutcome()
    {
    }

    /// <summary>Indicates successful registration.</summary>
    public sealed record Success(RegisteredGameResponse Game) : GameRegistrationOutcome;

    /// <summary>Indicates validation failure.</summary>
    public sealed record ValidationFailure(IReadOnlyDictionary<string, string[]> Errors) : GameRegistrationOutcome;
}

/// <summary>Represents a game returned after registration.</summary>
public sealed record RegisteredGameResponse(
    Guid Id,
    string Title,
    string Description,
    string Genre,
    decimal Price,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc);
