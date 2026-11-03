using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FCG.Application.Games;
using FCG.Application.Purchases;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using CatalogAPI.Messaging;
using CatalogAPI.Integrations;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CatalogAPI.Tests.Purchases;

public sealed class PurchaseEndpointTests
{
    [Fact]
    public async Task Readiness_fails_without_owned_database_while_liveness_stays_available()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE Games");
        }
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var body = await ready.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SQLite", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Games", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Purchase_route_uses_token_identity_and_catalog_price()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        var admin = Guid.NewGuid();
        var user = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(admin, "admin@example.com", "Administrator"));
        var gameResponse = await client.PostAsJsonAsync("/api/admin/games",
            new CreateGameCommand("Game", "Description", "Action", 19.95m));
        Assert.Equal(HttpStatusCode.Created, gameResponse.StatusCode);
        var game = await gameResponse.Content.ReadFromJsonAsync<GameRegistrationResponse>();
        Assert.NotNull(game);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(user, "user@example.com", "User"));
        var response = await client.PostAsync($"/api/library/me/purchases/{game.Id}", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var order = Assert.Single(await app.OrdersAsync());
        Assert.Equal(user, order.UserId);
        Assert.Equal(game.Id, order.GameId);
        Assert.Equal(19.95m, order.Price);
        Assert.Equal("user@example.com", order.RecipientEmail);
    }

    [Fact]
    public async Task Purchase_route_rejects_anonymous_requests_without_publishing()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        var response = await client.PostAsync($"/api/library/me/purchases/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await app.OrdersAsync());
    }

    [Fact]
    public async Task Administrator_can_associate_game_only_with_existing_user()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        var admin = Guid.NewGuid();
        var target = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(admin, "admin@example.com", "Administrator"));
        var gameResponse = await client.PostAsJsonAsync("/api/admin/games",
            new CreateGameCommand("Grant Game", "Description", "Action", 0m));
        var game = await gameResponse.Content.ReadFromJsonAsync<GameRegistrationResponse>();
        Assert.NotNull(game);

        app.Users.Result = UserLookupResult.NotFound;
        var missing = await client.PostAsync($"/api/admin/users/{target}/games/{game.Id}", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        app.Users.Result = UserLookupResult.Found;
        var granted = await client.PostAsync($"/api/admin/users/{target}/games/{game.Id}", null);
        Assert.Equal(HttpStatusCode.Created, granted.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(target, "user@example.com", "User"));
        var library = await client.GetFromJsonAsync<LibraryItem[]>('/' + "api/library/me");
        Assert.Contains(library!, item => item.GameId == game.Id);
        var denied = await client.PostAsync($"/api/admin/users/{target}/games/{game.Id}", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private sealed record GameRegistrationResponse(Guid Id);
    private sealed record LibraryItem(Guid GameId);

    private sealed class TestApp : WebApplicationFactory<global::Program>
    {
        private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"catalog-wave2-{Guid.NewGuid():N}.db");
        private readonly byte[] key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(48);
        private readonly Dictionary<string, string?> previousEnvironment = new();
        public TestUserLookup Users { get; } = new();

        public TestApp()
        {
            SetEnvironment("ConnectionStrings__Catalog", $"Data Source={dbPath}");
            SetEnvironment("Jwt__Issuer", "catalog-wave2-tests");
            SetEnvironment("Jwt__Audience", "catalog-wave2-tests");
            SetEnvironment("Jwt__SigningKey", Convert.ToBase64String(key));
        }

        private void SetEnvironment(string name, string value)
        {
            previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = $"Data Source={dbPath}",
                ["Jwt:Issuer"] = "catalog-wave2-tests",
                ["Jwt:Audience"] = "catalog-wave2-tests",
                ["Jwt:SigningKey"] = Convert.ToBase64String(key)
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserLookup>();
                services.AddSingleton<IUserLookup>(Users);
                var worker = services.SingleOrDefault(x => x.ServiceType == typeof(IHostedService) &&
                    x.ImplementationType == typeof(PaymentResultConsumer));
                if (worker is not null) services.Remove(worker);
                var relay = services.SingleOrDefault(x => x.ServiceType == typeof(IHostedService) &&
                    x.ImplementationType == typeof(OrderOutboxRelay));
                if (relay is not null) services.Remove(relay);
            });
        }

        public async Task<IReadOnlyList<OrderPlacedEvent>> OrdersAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var payloads = await db.OrderOutbox.AsNoTracking().Select(x => x.Payload).ToListAsync();
            return payloads.Select(x => JsonSerializer.Deserialize<OrderPlacedEvent>(x)!).ToList();
        }

        public string Token(Guid id, string email, string role)
        {
            var token = new JwtSecurityToken(
                issuer: "catalog-wave2-tests", audience: "catalog-wave2-tests",
                claims:
                [
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role)
                ],
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Convert.ToBase64String(key))), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            foreach (var (name, value) in previousEnvironment)
                Environment.SetEnvironmentVariable(name, value);
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private sealed class TestUserLookup : IUserLookup
    {
        public UserLookupResult Result { get; set; } = UserLookupResult.Found;
        public Task<UserLookupResult> FindAsync(Guid userId, string bearerToken, CancellationToken ct) => Task.FromResult(Result);
    }
}
