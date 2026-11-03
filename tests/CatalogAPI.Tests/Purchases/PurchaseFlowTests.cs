using FCG.Application.Games;
using FCG.Application.Library;
using FCG.Application.Purchases;
using FCG.Domain.Games;
using FCG.Domain.Library;
using FCG.Domain.Purchases;
using System.Text.Json;

namespace CatalogAPI.Tests.Purchases;

public sealed class PurchaseFlowTests
{
    [Fact]
    public async Task Purchase_uses_catalog_price_and_claim_identity()
    {
        var fixture = new Fixture();
        var result = await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);

        Assert.Equal(PurchasePlacementKind.Accepted, result);
        Assert.Single(fixture.Pending.Outbox);
        var order = JsonSerializer.Deserialize<OrderPlacedEvent>(fixture.Pending.Outbox[0].Payload)!;
        Assert.Equal(fixture.Pending.Items[0].Id, order.OrderId);
        Assert.NotEqual(Guid.Empty, order.EventId);
        Assert.Equal(fixture.UserId, order.UserId);
        Assert.Equal(fixture.Game.Id, order.GameId);
        Assert.Equal(12.50m, order.Price);
        Assert.Equal(fixture.Email, order.RecipientEmail);
        Assert.Empty(fixture.Library.Acquisitions);
    }

    [Fact]
    public async Task Missing_or_owned_game_never_publishes_an_order()
    {
        var fixture = new Fixture();
        Assert.Equal(PurchasePlacementKind.GameNotFound,
            await fixture.Purchases.PlaceAsync(fixture.UserId, Guid.NewGuid(), fixture.Email, default));
        fixture.Library.Acquisitions.Add(AcquiredGame.Acquire(fixture.UserId, fixture.Game.Id, DateTime.UtcNow));
        Assert.Equal(PurchasePlacementKind.AlreadyAcquired,
            await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default));
        Assert.Empty(fixture.Pending.Outbox);
    }

    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Rejected", false)]
    public async Task Result_changes_library_only_for_approval(string status, bool acquired)
    {
        var fixture = new Fixture();
        await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);

        var outcome = await fixture.Results.HandleAsync(
            fixture.Payment(status), default);

        Assert.Equal(PaymentApplicationKind.Processed, outcome);
        Assert.Equal(acquired, fixture.Library.Acquisitions.Count == 1);
        Assert.Empty(fixture.Pending.Items);
    }

    [Fact]
    public async Task Approval_for_another_user_game_or_email_does_not_grant_ownership()
    {
        var fixture = new Fixture();
        await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);
        foreach (var payment in new[]
        {
            fixture.Payment("Approved") with { UserId = Guid.NewGuid() },
            fixture.Payment("Approved") with { GameId = Guid.NewGuid() },
            fixture.Payment("Approved") with { RecipientEmail = "other@example.com" },
            fixture.Payment("Approved") with { OrderId = Guid.NewGuid() }
        })
            Assert.Equal(PaymentApplicationKind.NoMatchingPurchase,
                await fixture.Results.HandleAsync(payment, default));

        Assert.Empty(fixture.Library.Acquisitions);
        Assert.Single(fixture.Pending.Items);
    }

    [Fact]
    public async Task Repeated_approval_does_not_add_a_second_acquisition()
    {
        var fixture = new Fixture();
        await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);
        var payment = fixture.Payment("Approved");
        Assert.Equal(PaymentApplicationKind.Processed, await fixture.Results.HandleAsync(payment, default));
        Assert.Equal(PaymentApplicationKind.AlreadyProcessed, await fixture.Results.HandleAsync(payment, default));
        Assert.Single(fixture.Library.Acquisitions);
    }

    [Fact]
    public async Task Delayed_result_from_an_old_attempt_cannot_settle_a_new_attempt()
    {
        var fixture = new Fixture();
        await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);
        var oldPayment = fixture.Payment("Rejected");
        Assert.Equal(PaymentApplicationKind.Processed, await fixture.Results.HandleAsync(oldPayment, default));
        await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);

        Assert.Equal(PaymentApplicationKind.AlreadyProcessed,
            await fixture.Results.HandleAsync(oldPayment with { EventId = Guid.NewGuid() }, default));
        Assert.Empty(fixture.Library.Acquisitions);
        Assert.Single(fixture.Pending.Items);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string Email { get; } = "user@example.com";
        public Game Game { get; } = Game.Create("Game", "Description", "Genre", Guid.NewGuid(), DateTime.UtcNow, 12.50m);
        public FakePending Pending { get; } = new();
        public FakeLibrary Library { get; } = new();
        public PurchaseService Purchases { get; }
        public PaymentResultService Results { get; }

        public Fixture()
        {
            Purchases = new PurchaseService(new FakeGames(Game), Library, Pending);
            Results = new PaymentResultService(new FakeGames(Game), Library, Pending);
        }

        public PaymentProcessedEvent Payment(string status)
        {
            var purchase = Assert.Single(Pending.Items);
            return new PaymentProcessedEvent(Guid.NewGuid(), purchase.Id, UserId, Game.Id, status, Email);
        }
    }

    private sealed class FakeGames(Game game) : IGameRepository
    {
        public Task<IReadOnlyList<Game>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Game>>([game]);
        public Task<Game?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Game?>(id == game.Id ? game : null);
        public Task<Game?> FindTrackedByIdAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task<bool> HasOwnershipOrPendingPurchaseAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Game value, CancellationToken ct) => Task.CompletedTask;
        public void Remove(Game value) { }
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FakeLibrary : ILibraryRepository
    {
        public List<AcquiredGame> Acquisitions { get; } = [];
        public Task<IReadOnlyList<LibraryItemResponse>> FindByUserIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LibraryItemResponse>>([]);
        public Task<bool> ExistsAsync(Guid user, Guid game, CancellationToken ct) =>
            Task.FromResult(Acquisitions.Any(x => x.UserId == user && x.GameId == game));
        public Task AddAsync(AcquiredGame value, CancellationToken ct) { Acquisitions.Add(value); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FakePending : IPendingPurchaseRepository
    {
        public List<PendingPurchase> Items { get; } = [];
        public List<OrderOutboxMessage> Outbox { get; } = [];
        public List<ProcessedPaymentEvent> Processed { get; } = [];
        public Task<PendingPurchase?> FindAsync(Guid user, Guid game, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.UserId == user && x.GameId == game));
        public Task<PendingPurchase?> FindByOrderIdAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.Id == orderId));
        public Task AddAsync(PendingPurchase value, CancellationToken ct) { Items.Add(value); return Task.CompletedTask; }
        public Task AddOutboxAsync(OrderOutboxMessage value, CancellationToken ct) { Outbox.Add(value); return Task.CompletedTask; }
        public Task<bool> WasProcessedAsync(Guid eventId, Guid orderId, CancellationToken ct) =>
            Task.FromResult(Processed.Any(x => x.EventId == eventId || x.OrderId == orderId));
        public Task AddProcessedAsync(ProcessedPaymentEvent value, CancellationToken ct) { Processed.Add(value); return Task.CompletedTask; }
        public Task<int> DeleteProcessedBeforeAsync(DateTime cutoffUtc, CancellationToken ct)
        {
            var removed = Processed.RemoveAll(x => x.ProcessedAtUtc < cutoffUtc);
            return Task.FromResult(removed);
        }
        public void Remove(PendingPurchase value) => Items.Remove(value);
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }
}
