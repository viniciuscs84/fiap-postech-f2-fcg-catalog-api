using System.Security.Claims;
using System.Text;
using FCG.Application.Authentication;
using FCG.Application.Games;
using FCG.Application.Library;
using FCG.Application.Promotions;
using FCG.Infrastructure.Games;
using FCG.Infrastructure.Library;
using FCG.Infrastructure.Persistence;
using FCG.Infrastructure.Promotions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Catalog") ?? "Data Source=catalog.db"));
builder.Services.AddScoped<IGameRepository, EfGameRepository>();
builder.Services.AddScoped<ILibraryRepository, EfLibraryRepository>();
builder.Services.AddScoped<IPromotionRepository, EfPromotionRepository>();
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
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

app.Run();

public partial class Program { }
