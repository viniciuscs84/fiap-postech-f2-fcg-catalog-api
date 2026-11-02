using FCG.Application.Games;
using FCG.Application.Library;
using FCG.Application.Purchases;
using FCG.Domain.Games;
using FCG.Domain.Library;
using FCG.Domain.Purchases;

namespace CatalogAPI.Tests.Purchases;

public sealed class PurchaseFlowTests
{
    [Fact]
    public async Task Purchase_uses_catalog_price_and_claim_identity()
    {
        var fixture = new Fixture();
        var result = await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);

        Assert.Equal(PurchasePlacementKind.Accepted, result);
        Assert.Single(fixture.Publisher.Events);
        var order = fixture.Publisher.Events[0];
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
        Assert.Empty(fixture.Publisher.Events);
    }

    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Rejected", false)]
    public async Task Result_changes_library_only_for_approval(string status, bool acquired)
    {
        var fixture = new Fixture();
        await fixture.Purchases.PlaceAsync(fixture.UserId, fixture.Game.Id, fixture.Email, default);

        var outcome = await fixture.Results.HandleAsync(
            new PaymentProcessedEvent(fixture.UserId, fixture.Game.Id, status, fixture.Email), default);

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
            new PaymentProcessedEvent(Guid.NewGuid(), fixture.Game.Id, "Approved", fixture.Email),
            new PaymentProcessedEvent(fixture.UserId, Guid.NewGuid(), "Approved", fixture.Email),
            new PaymentProcessedEvent(fixture.UserId, fixture.Game.Id, "Approved", "other@example.com")
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
        var payment = new PaymentProcessedEvent(fixture.UserId, fixture.Game.Id, "Approved", fixture.Email);
        Assert.Equal(PaymentApplicationKind.Processed, await fixture.Results.HandleAsync(payment, default));
        Assert.Equal(PaymentApplicationKind.NoMatchingPurchase, await fixture.Results.HandleAsync(payment, default));
        Assert.Single(fixture.Library.Acquisitions);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string Email { get; } = "user@example.com";
        public Game Game { get; } = Game.Create("Game", "Description", "Genre", Guid.NewGuid(), DateTime.UtcNow, 12.50m);
        public FakePending Pending { get; } = new();
        public FakeLibrary Library { get; } = new();
        public FakePublisher Publisher { get; } = new();
        public PurchaseService Purchases { get; }
        public PaymentResultService Results { get; }

        public Fixture()
        {
            Purchases = new PurchaseService(new FakeGames(Game), Library, Pending, Publisher);
            Results = new PaymentResultService(new FakeGames(Game), Library, Pending);
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
        public Task<PendingPurchase?> FindAsync(Guid user, Guid game, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.UserId == user && x.GameId == game));
        public Task AddAsync(PendingPurchase value, CancellationToken ct) { Items.Add(value); return Task.CompletedTask; }
        public void Remove(PendingPurchase value) => Items.Remove(value);
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FakePublisher : IOrderEventPublisher
    {
        public List<OrderPlacedEvent> Events { get; } = [];
        public Task PublishAsync(OrderPlacedEvent value, CancellationToken ct) { Events.Add(value); return Task.CompletedTask; }
    }
}
