using FCG.Application.Games;
using FCG.Domain.Games;

namespace CatalogAPI.Tests.Games;

public sealed class GameAdministrationServiceTests
{
    [Fact]
    public async Task Update_changes_the_existing_game()
    {
        var game = NewGame();
        var repository = new FakeGameRepository(game);
        var service = new GameAdministrationService(repository);

        var result = await service.UpdateAsync(game.Id,
            new UpdateGameCommand("Updated", "Updated description", "Strategy", 15m), CancellationToken.None);

        Assert.Equal(GameMutationKind.Success, result.Kind);
        Assert.Equal("Updated", game.Title);
        Assert.Equal(15m, game.Price);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Delete_does_not_remove_an_acquired_game()
    {
        var game = NewGame();
        var repository = new FakeGameRepository(game) { IsAcquired = true };
        var service = new GameAdministrationService(repository);

        var result = await service.DeleteAsync(game.Id, CancellationToken.None);

        Assert.Equal(GameMutationKind.Conflict, result.Kind);
        Assert.False(repository.Removed);
    }

    private static Game NewGame() =>
        Game.Create("Game", "Description", "Action", Guid.NewGuid(), DateTime.UtcNow, 10m);

    private sealed class FakeGameRepository(Game game) : IGameRepository
    {
        public bool IsAcquired { get; set; }
        public bool Removed { get; private set; }
        public int SaveCount { get; private set; }
        public Task<IReadOnlyList<Game>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Game>>([game]);
        public Task<Game?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == game.Id ? game : null);
        public Task<Game?> FindTrackedByIdAsync(Guid id, CancellationToken ct) =>
            FindByIdAsync(id, ct);
        public Task<bool> HasOwnershipOrPendingPurchaseAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(IsAcquired);
        public Task AddAsync(Game item, CancellationToken ct) => Task.CompletedTask;
        public void Remove(Game item) => Removed = true;
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
