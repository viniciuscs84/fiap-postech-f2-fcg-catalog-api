namespace FCG.Application.Games;

/// <summary>Contains the data required to create a game.</summary>
public sealed record CreateGameCommand(string Title, string Description, string Genre, decimal Price);
