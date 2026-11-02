# CatalogAPI

Phase 2 catalog service for game CRUD, Phase 1 promotion creation, and private acquired-game library reads. Game prices are catalog-owned decimal values used by the planned order event.

## Build and test

```powershell
dotnet build src/CatalogAPI/CatalogAPI.csproj
dotnet test tests/CatalogAPI.Tests/CatalogAPI.Tests.csproj
```

## Configuration

Set `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKey` to match the values used by UsersAPI. Do not commit the signing key. `ConnectionStrings__Catalog` optionally selects the service's SQLite database; the default is `catalog.db`. The service creates its local schema on startup.

Start with `dotnet run --project src/CatalogAPI/CatalogAPI.csproj`. `/health` reports process availability. `/swagger` shows the current HTTP endpoints.

The Phase 1 direct self-service acquisition endpoint is intentionally absent: purchase initiation and payment-result handling belong to Wave 2. Administrative association still requires a cross-service user-existence strategy and is not yet exposed.
