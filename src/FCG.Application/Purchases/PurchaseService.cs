using System.Text.Json;
using FCG.Application.Games;
using FCG.Application.Library;
using FCG.Domain.Purchases;

namespace FCG.Application.Purchases;

public sealed record OrderPlacedEvent(
    Guid EventId, Guid OrderId, Guid UserId, Guid GameId, decimal Price, string RecipientEmail, int Version = 2);
public sealed record PaymentProcessedEvent(
    Guid EventId, Guid OrderId, Guid UserId, Guid GameId, string Status, string RecipientEmail, int Version = 2);

public interface IPendingPurchaseRepository
{
    Task<PendingPurchase?> FindAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
    Task<PendingPurchase?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task AddAsync(PendingPurchase value, CancellationToken cancellationToken);
    Task AddOutboxAsync(OrderOutboxMessage value, CancellationToken cancellationToken);
    Task<bool> WasProcessedAsync(Guid eventId, Guid orderId, CancellationToken cancellationToken);
    Task AddProcessedAsync(ProcessedPaymentEvent value, CancellationToken cancellationToken);
    Task<int> DeleteProcessedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
    void Remove(PendingPurchase value);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOrderOutboxRepository
{
    Task<IReadOnlyList<OrderOutboxMessage>> FindPendingAsync(int limit, CancellationToken cancellationToken);
    Task<OrderOutboxMessage?> FindReplayEligibleAsync(Guid eventId, DateTime confirmedAfterUtc, CancellationToken cancellationToken);
    Task<int> DeleteConfirmedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public enum PurchasePlacementKind { Accepted, GameNotFound, AlreadyAcquired, AlreadyPending }

public sealed class PurchaseService(
    IGameRepository games, ILibraryRepository library, IPendingPurchaseRepository pending)
{
    public async Task<PurchasePlacementKind> PlaceAsync(Guid userId, Guid gameId, string recipientEmail, CancellationToken ct)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("A valid authenticated user and email are required.");
        var game = await games.FindByIdAsync(gameId, ct);
        if (game is null) return PurchasePlacementKind.GameNotFound;
        if (await library.ExistsAsync(userId, gameId, ct)) return PurchasePlacementKind.AlreadyAcquired;
        if (await pending.FindAsync(userId, gameId, ct) is not null) return PurchasePlacementKind.AlreadyPending;

        var purchase = PendingPurchase.Create(userId, gameId, game.Price, recipientEmail, DateTime.UtcNow);
        var order = new OrderPlacedEvent(
            Guid.NewGuid(), purchase.Id, userId, gameId, game.Price, recipientEmail);
        await pending.AddAsync(purchase, ct);
        await pending.AddOutboxAsync(OrderOutboxMessage.Create(
            order.EventId, purchase.Id, JsonSerializer.Serialize(order), DateTime.UtcNow), ct);
        await pending.SaveChangesAsync(ct);
        return PurchasePlacementKind.Accepted;
    }
}

public enum PaymentApplicationKind { Processed, AlreadyProcessed, NoMatchingPurchase, InvalidStatus }

public sealed class PaymentResultService(IGameRepository games, ILibraryRepository library, IPendingPurchaseRepository pending)
{
    public async Task<PaymentApplicationKind> HandleAsync(PaymentProcessedEvent payment, CancellationToken ct)
    {
        if (payment.Status is not ("Approved" or "Rejected")) return PaymentApplicationKind.InvalidStatus;
        if (payment.EventId == Guid.Empty || payment.OrderId == Guid.Empty || payment.Version != 2)
            return PaymentApplicationKind.InvalidStatus;
        if (await pending.WasProcessedAsync(payment.EventId, payment.OrderId, ct))
            return PaymentApplicationKind.AlreadyProcessed;
        var purchase = await pending.FindByOrderIdAsync(payment.OrderId, ct);
        if (purchase is null || purchase.UserId != payment.UserId || purchase.GameId != payment.GameId ||
            !string.Equals(purchase.RecipientEmail, payment.RecipientEmail, StringComparison.OrdinalIgnoreCase))
            return PaymentApplicationKind.NoMatchingPurchase;

        if (payment.Status == "Approved")
        {
            if (await games.FindByIdAsync(payment.GameId, ct) is null)
                return PaymentApplicationKind.NoMatchingPurchase;
            if (!await library.ExistsAsync(payment.UserId, payment.GameId, ct))
                await library.AddAsync(FCG.Domain.Library.AcquiredGame.Acquire(
                    payment.UserId, payment.GameId, DateTime.UtcNow), ct);
        }
        await pending.AddProcessedAsync(ProcessedPaymentEvent.Create(
            payment.EventId, payment.OrderId, DateTime.UtcNow), ct);
        pending.Remove(purchase);
        await pending.SaveChangesAsync(ct);
        return PaymentApplicationKind.Processed;
    }
}
