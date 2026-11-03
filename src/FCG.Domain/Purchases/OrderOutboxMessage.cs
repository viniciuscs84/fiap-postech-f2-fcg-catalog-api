namespace FCG.Domain.Purchases;

public sealed class OrderOutboxMessage
{
    private OrderOutboxMessage() { }

    public Guid EventId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public int AttemptCount { get; private set; }

    public static OrderOutboxMessage Create(Guid eventId, Guid orderId, string payload, DateTime createdAtUtc)
    {
        if (eventId == Guid.Empty || orderId == Guid.Empty) throw new ArgumentException("Event and order identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        return new OrderOutboxMessage
        {
            EventId = eventId,
            OrderId = orderId,
            Payload = payload,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }

    public void RecordAttempt(DateTime attemptedAtUtc)
    {
        LastAttemptAtUtc = DateTime.SpecifyKind(attemptedAtUtc, DateTimeKind.Utc);
        AttemptCount++;
    }

    public void MarkPublished(DateTime publishedAtUtc)
    {
        PublishedAtUtc = DateTime.SpecifyKind(publishedAtUtc, DateTimeKind.Utc);
    }

    public void ResetForReplay() => PublishedAtUtc = null;
}
