namespace FCG.Domain.Games;

/// <summary>Represents a game available in the catalog.</summary>
public sealed class Game
{
    private Game()
    {
    }

    /// <summary>Gets the unique game identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the game title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the game description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the game genre.</summary>
    public string Genre { get; private set; } = string.Empty;

    /// <summary>Gets the catalog price used by the purchase event.</summary>
    public decimal Price { get; private set; }

    /// <summary>Gets the identifier of the administrator who created the game.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Gets the creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Creates a catalog game.</summary>
    /// <param name="title">Game title.</param>
    /// <param name="description">Game description.</param>
    /// <param name="genre">Game genre.</param>
    /// <param name="createdByUserId">Creating administrator identifier.</param>
    /// <param name="createdAtUtc">Creation timestamp.</param>
    /// <param name="price">Price used by the purchase flow.</param>
    /// <returns>A new game.</returns>
    public static Game Create(string title, string description, string genre, Guid createdByUserId, DateTime createdAtUtc, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(genre);
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("A creator user id is required.", nameof(createdByUserId));
        }

        return new Game
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description.Trim(),
            Genre = genre.Trim(),
            Price = price,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }

    /// <summary>Updates the catalog fields used by administration and purchasing.</summary>
    public void Update(string title, string description, string genre, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(genre);
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        Title = title.Trim();
        Description = description.Trim();
        Genre = genre.Trim();
        Price = price;
    }
}
