using FCG.Domain.Games;

namespace CatalogAPI.Tests.Games;

public sealed class GameLifecycleTests
{
    [Fact]
    public void Create_keeps_catalog_price()
    {
        var game = Game.Create("Game", "Description", "Action", Guid.NewGuid(), DateTime.UtcNow, 29.90m);

        Assert.Equal(29.90m, game.Price);
    }

    [Fact]
    public void Update_changes_catalog_fields_and_price()
    {
        var game = Game.Create("Old", "Old description", "Action", Guid.NewGuid(), DateTime.UtcNow, 29.90m);

        game.Update("New", "New description", "Strategy", 39.90m);

        Assert.Equal("New", game.Title);
        Assert.Equal("New description", game.Description);
        Assert.Equal("Strategy", game.Genre);
        Assert.Equal(39.90m, game.Price);
    }

    [Fact]
    public void Negative_price_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Game.Create("Game", "Description", "Action", Guid.NewGuid(), DateTime.UtcNow, -1m));
    }
}
