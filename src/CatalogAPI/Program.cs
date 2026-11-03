using System.Security.Claims;
using System.Text;
using FCG.Application.Authentication;
using FCG.Application.Games;
using FCG.Application.Library;
using FCG.Application.Promotions;
using FCG.Application.Purchases;
using CatalogAPI.Messaging;
using CatalogAPI.Integrations;
using System.Net.Http.Headers;
using FCG.Infrastructure.Games;
using FCG.Infrastructure.Library;
using FCG.Infrastructure.Persistence;
using FCG.Infrastructure.Promotions;
using FCG.Infrastructure.Purchases;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Catalog") ?? "Data Source=catalog.db"));
builder.Services.AddScoped<IGameRepository, EfGameRepository>();
builder.Services.AddScoped<ILibraryRepository, EfLibraryRepository>();
builder.Services.AddScoped<IPromotionRepository, EfPromotionRepository>();
builder.Services.AddScoped<IPendingPurchaseRepository, EfPendingPurchaseRepository>();
builder.Services.AddScoped<IOrderOutboxRepository, EfOrderOutboxRepository>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<PaymentResultService>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<RabbitMqOrderPublisher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EventMetrics>();
builder.Services.AddHostedService<EventMetricsWorker>();
builder.Services.AddHostedService<OrderOutboxRelay>();
builder.Services.AddHostedService<PaymentResultConsumer>();
builder.Services.AddHttpClient<IUserLookup, HttpUserLookup>(client =>
{
    if (Uri.TryCreate(builder.Configuration["UsersApi:BaseUrl"], UriKind.Absolute, out var address) &&
        address.Scheme is "http" or "https")
        client.BaseAddress = address;
    client.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IGameRegistrationService, GameRegistrationService>();
builder.Services.AddScoped<IGameCatalogService, GameCatalogService>();
builder.Services.AddScoped<IGameAdministrationService, GameAdministrationService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IPromotionRegistrationService, PromotionRegistrationService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) ||
    string.IsNullOrWhiteSpace(jwt.SigningKey))
    throw new InvalidOperationException("JWT issuer, audience, and signing key must be configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserOrAdministrator", policy => policy.RequireRole("User", "Administrator"));
    options.AddPolicy("AdministratorOnly", policy => policy.RequireRole("Administrator"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    // EnsureCreated does not add new tables to databases produced by Wave 1.
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS PendingPurchases (
            Id TEXT NOT NULL CONSTRAINT PK_PendingPurchases PRIMARY KEY,
            UserId TEXT NOT NULL,
            GameId TEXT NOT NULL,
            Price TEXT NOT NULL,
            RecipientEmail TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            CONSTRAINT FK_PendingPurchases_Games_GameId FOREIGN KEY (GameId) REFERENCES Games (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_PendingPurchases_GameId ON PendingPurchases (GameId);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_PendingPurchases_UserId_GameId ON PendingPurchases (UserId, GameId);
        CREATE TABLE IF NOT EXISTS OrderOutbox (
            EventId TEXT NOT NULL CONSTRAINT PK_OrderOutbox PRIMARY KEY,
            OrderId TEXT NOT NULL,
            Payload TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            PublishedAtUtc TEXT NULL,
            LastAttemptAtUtc TEXT NULL,
            AttemptCount INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrderOutbox_OrderId ON OrderOutbox (OrderId);
        CREATE INDEX IF NOT EXISTS IX_OrderOutbox_PublishedAtUtc_CreatedAtUtc ON OrderOutbox (PublishedAtUtc, CreatedAtUtc);
        CREATE TABLE IF NOT EXISTS ProcessedPaymentEvents (
            EventId TEXT NOT NULL CONSTRAINT PK_ProcessedPaymentEvents PRIMARY KEY,
            OrderId TEXT NOT NULL,
            ProcessedAtUtc TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_ProcessedPaymentEvents_OrderId ON ProcessedPaymentEvents (OrderId);
        """);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "live", service = "CatalogAPI" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live", service = "CatalogAPI" }));
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken requestCt) =>
{
    using var budget = CancellationTokenSource.CreateLinkedTokenSource(requestCt);
    budget.CancelAfter(TimeSpan.FromSeconds(2));
    try
    {
        await db.Games.AnyAsync(budget.Token).WaitAsync(TimeSpan.FromSeconds(2), requestCt);
        return Results.Ok(new { status = "ready", service = "CatalogAPI" });
    }
    catch (Exception) { return Results.Json(new { status = "unavailable", service = "CatalogAPI" }, statusCode: 503); }
});

app.MapGet("/api/games", [Authorize(Policy = "UserOrAdministrator")] async (
    IGameCatalogService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));

app.MapGet("/api/games/{gameId:guid}", [Authorize(Policy = "UserOrAdministrator")] async (
    Guid gameId, IGameCatalogService service, CancellationToken ct) =>
{
    var game = await service.GetByIdAsync(gameId, ct);
    return game is null ? Results.NotFound() : Results.Ok(game);
});

app.MapPost("/api/admin/games", [Authorize(Policy = "AdministratorOnly")] async (
    CreateGameCommand command, ClaimsPrincipal principal, IGameRegistrationService service, CancellationToken ct) =>
{
    if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        return Results.Unauthorized();
    var result = await service.RegisterAsync(command, adminId, ct);
    return result switch
    {
        GameRegistrationOutcome.Success success => Results.Created($"/api/games/{success.Game.Id}", success.Game),
        GameRegistrationOutcome.ValidationFailure failure => Results.ValidationProblem(
            failure.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)),
        _ => throw new InvalidOperationException("Unexpected game registration outcome.")
    };
});

app.MapPut("/api/admin/games/{gameId:guid}", [Authorize(Policy = "AdministratorOnly")] async (
    Guid gameId, UpdateGameCommand command, IGameAdministrationService service, CancellationToken ct) =>
{
    var result = await service.UpdateAsync(gameId, command, ct);
    return result.Kind switch
    {
        GameMutationKind.Success => Results.Ok(result.Game),
        GameMutationKind.NotFound => Results.NotFound(),
        GameMutationKind.ValidationFailure => Results.ValidationProblem(result.Errors!.ToDictionary(
            pair => pair.Key, pair => pair.Value)),
        _ => throw new InvalidOperationException("Unexpected game update outcome.")
    };
});

app.MapDelete("/api/admin/games/{gameId:guid}", [Authorize(Policy = "AdministratorOnly")] async (
    Guid gameId, IGameAdministrationService service, CancellationToken ct) =>
{
    var result = await service.DeleteAsync(gameId, ct);
    return result.Kind switch
    {
        GameMutationKind.Success => Results.NoContent(),
        GameMutationKind.NotFound => Results.NotFound(),
        GameMutationKind.Conflict => Results.Conflict(new { message = "Um jogo adquirido não pode ser excluído." }),
        _ => throw new InvalidOperationException("Unexpected game delete outcome.")
    };
});

app.MapPost("/api/admin/promotions", [Authorize(Policy = "AdministratorOnly")] async (
    CreatePromotionCommand command, ClaimsPrincipal principal, IPromotionRegistrationService service, CancellationToken ct) =>
{
    if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        return Results.Unauthorized();
    var result = await service.RegisterAsync(command, adminId, ct);
    return result switch
    {
        PromotionRegistrationOutcome.Success success => Results.Created($"/api/admin/promotions/{success.Promotion.Id}", success.Promotion),
        PromotionRegistrationOutcome.ValidationFailure failure => Results.ValidationProblem(
            failure.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)),
        PromotionRegistrationOutcome.Conflict => Results.Conflict(new { message = "Já existe uma promoção com este código." }),
        _ => throw new InvalidOperationException("Unexpected promotion registration outcome.")
    };
});

app.MapGet("/api/library/me", [Authorize(Policy = "UserOrAdministrator")] async (
    ClaimsPrincipal principal, ILibraryService service, CancellationToken ct) =>
{
    if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        return Results.Unauthorized();
    return Results.Ok(await service.GetMyLibraryAsync(userId, ct));
});

app.MapPost("/api/admin/users/{userId:guid}/games/{gameId:guid}", [Authorize(Policy = "AdministratorOnly")] async (
    Guid userId, Guid gameId, HttpContext context, IUserLookup users, ILibraryService library, CancellationToken ct) =>
{
    if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization) ||
        !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(authorization.Parameter))
        return Results.Unauthorized();

    var user = await users.FindAsync(userId, authorization.Parameter, ct);
    if (user == UserLookupResult.NotFound) return Results.NotFound();
    if (user == UserLookupResult.Unavailable) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    var result = await library.AcquireAsync(userId, gameId, ct);
    return result switch
    {
        GameAcquisitionOutcome.Success success => Results.Created($"/api/admin/users/{userId}", success.Item),
        GameAcquisitionOutcome.GameNotFound => Results.NotFound(),
        GameAcquisitionOutcome.AlreadyAcquired => Results.Conflict(),
        _ => throw new InvalidOperationException("Unexpected administrative acquisition outcome.")
    };
});

app.MapPost("/api/library/me/purchases/{gameId:guid}", [Authorize(Policy = "UserOrAdministrator")] async (
    Guid gameId, ClaimsPrincipal principal, PurchaseService service, CancellationToken ct) =>
{
    if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
        string.IsNullOrWhiteSpace(principal.FindFirstValue(ClaimTypes.Email)))
        return Results.Unauthorized();
    var result = await service.PlaceAsync(userId, gameId, principal.FindFirstValue(ClaimTypes.Email)!, ct);
    return result switch
    {
        PurchasePlacementKind.Accepted => Results.Accepted(),
        PurchasePlacementKind.GameNotFound => Results.NotFound(),
        PurchasePlacementKind.AlreadyAcquired => Results.Conflict(),
        PurchasePlacementKind.AlreadyPending => Results.Conflict(),
        _ => throw new InvalidOperationException("Unexpected purchase result.")
    };
});

app.Run();

public partial class Program { }
