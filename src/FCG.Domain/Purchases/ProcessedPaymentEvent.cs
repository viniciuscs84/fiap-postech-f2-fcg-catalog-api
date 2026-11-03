namespace FCG.Domain.Purchases;

public sealed class ProcessedPaymentEvent
{
    private ProcessedPaymentEvent() { }

    public Guid EventId { get; private set; }
    public Guid OrderId { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }

    public static ProcessedPaymentEvent Create(Guid eventId, Guid orderId, DateTime processedAtUtc)
    {
        if (eventId == Guid.Empty || orderId == Guid.Empty) throw new ArgumentException("Event and order identifiers are required.");
        return new ProcessedPaymentEvent
        {
            EventId = eventId,
            OrderId = orderId,
            ProcessedAtUtc = DateTime.SpecifyKind(processedAtUtc, DateTimeKind.Utc)
        };
    }
}
