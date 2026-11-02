using FCG.Application.Games;
using FCG.Application.Library;
using FCG.Domain.Purchases;

namespace FCG.Application.Purchases;

public sealed record OrderPlacedEvent(Guid UserId, Guid GameId, decimal Price, string RecipientEmail);
public sealed record PaymentProcessedEvent(Guid UserId, Guid GameId, string Status, string RecipientEmail);

public interface IOrderEventPublisher
{
    Task PublishAsync(OrderPlacedEvent value, CancellationToken cancellationToken);
}

public interface IPendingPurchaseRepository
{
    Task<PendingPurchase?> FindAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
    Task AddAsync(PendingPurchase value, CancellationToken cancellationToken);
    void Remove(PendingPurchase value);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public enum PurchasePlacementKind { Accepted, GameNotFound, AlreadyAcquired, AlreadyPending }

public sealed class PurchaseService(
    IGameRepository games, ILibraryRepository library, IPendingPurchaseRepository pending,
    IOrderEventPublisher publisher)
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
        await pending.AddAsync(purchase, ct);
        await pending.SaveChangesAsync(ct);
        try
        {
            await publisher.PublishAsync(new OrderPlacedEvent(userId, gameId, game.Price, recipientEmail), ct);
        }
        catch
        {
            pending.Remove(purchase);
            await pending.SaveChangesAsync(ct);
            throw;
        }
        return PurchasePlacementKind.Accepted;
    }
}

public enum PaymentApplicationKind { Processed, NoMatchingPurchase, InvalidStatus }

public sealed class PaymentResultService(IGameRepository games, ILibraryRepository library, IPendingPurchaseRepository pending)
{
    public async Task<PaymentApplicationKind> HandleAsync(PaymentProcessedEvent payment, CancellationToken ct)
    {
        if (payment.Status is not ("Approved" or "Rejected")) return PaymentApplicationKind.InvalidStatus;
        var purchase = await pending.FindAsync(payment.UserId, payment.GameId, ct);
        if (purchase is null || !string.Equals(purchase.RecipientEmail, payment.RecipientEmail, StringComparison.OrdinalIgnoreCase))
            return PaymentApplicationKind.NoMatchingPurchase;

        if (payment.Status == "Approved")
        {
            if (await games.FindByIdAsync(payment.GameId, ct) is null)
                return PaymentApplicationKind.NoMatchingPurchase;
            if (!await library.ExistsAsync(payment.UserId, payment.GameId, ct))
                await library.AddAsync(FCG.Domain.Library.AcquiredGame.Acquire(
                    payment.UserId, payment.GameId, DateTime.UtcNow), ct);
        }
        pending.Remove(purchase);
        await pending.SaveChangesAsync(ct);
        return PaymentApplicationKind.Processed;
    }
}
