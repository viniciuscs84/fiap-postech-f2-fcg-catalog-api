namespace FCG.Domain.Purchases;

public sealed class PendingPurchase
{
    private PendingPurchase() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public decimal Price { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public static PendingPurchase Create(Guid userId, Guid gameId, decimal price, string recipientEmail, DateTime createdAtUtc)
    {
        if (userId == Guid.Empty || gameId == Guid.Empty) throw new ArgumentException("User and game are required.");
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        return new PendingPurchase
        {
            Id = Guid.NewGuid(), UserId = userId, GameId = gameId, Price = price,
            RecipientEmail = recipientEmail, CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }
}
