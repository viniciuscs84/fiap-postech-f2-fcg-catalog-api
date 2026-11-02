namespace FCG.Application.Library;

/// <summary>Represents an acquired game in a user's library response.</summary>
public sealed record LibraryItemResponse(
    Guid GameId,
    string Title,
    string Description,
    string Genre,
    DateTime AcquiredAtUtc);
