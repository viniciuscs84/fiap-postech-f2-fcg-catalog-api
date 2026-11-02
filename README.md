# CatalogAPI

Phase 2 catalog service for game CRUD, Phase 1 promotion creation, private acquired-game library reads, and event-driven purchase initiation. Game prices are catalog-owned decimal values used by the order event.

## Build and test

```powershell
dotnet build src/CatalogAPI/CatalogAPI.csproj
dotnet test tests/CatalogAPI.Tests/CatalogAPI.Tests.csproj
```

## Configuration

Set `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKey` to match the values used by UsersAPI. Do not commit the signing key. `ConnectionStrings__Catalog` optionally selects the service's SQLite database; the default is `catalog.db`. The service creates its local schema on startup.

Start with `dotnet run --project src/CatalogAPI/CatalogAPI.csproj`. `/health` reports process availability. `/swagger` shows the current HTTP endpoints.

## Purchase flow

`POST /api/library/me/purchases/{gameId}` requires a valid UsersAPI JWT. The user ID and recipient email come from its claims; the stored game supplies `Price`. A valid request records a pending purchase and publishes `OrderPlacedEvent` to the durable `payments.order-placed` RabbitMQ queue on the `fcg.events` direct exchange. The response is `202 Accepted`. The old direct self-service association endpoint remains absent.

The service consumes `PaymentProcessedEvent` from `catalog.payment-processed`. It adds the game to the correct private library only for a matching pending purchase with `Approved` status. A `Rejected` result removes the pending purchase without granting ownership. The pending record prevents an unrelated result from granting a game and is not an order-history feature. Game deletion is blocked while a purchase is pending or the game is owned.

Configure RabbitMQ through `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password`, and `RabbitMq__Exchange`. Supply the password from the runtime environment; do not commit it. The local defaults are `localhost:5672` and the RabbitMQ development user. The consumer retries when the broker is unavailable. Existing Wave 1 SQLite databases receive the pending-purchase table on startup.

The Phase 1 administrator association remains at `POST /api/admin/users/{userId}/games/{gameId}`. Set `UsersApi__BaseUrl` to the trusted UsersAPI root URL (for example, `http://localhost:8081/` or the internal Compose URL). CatalogAPI forwards only the administrator's bearer token to UsersAPI's protected user lookup. An unknown user returns `404`; an unavailable or unconfigured UsersAPI returns `503` without granting a game. Regular users cannot call the route.

The current publication path does not provide guaranteed delivery if the process stops between saving a pending purchase and publishing its event.

## Container preparation

`Dockerfile` publishes this service from its own repository into a .NET 8 runtime image. The image runs as the built-in non-root user, listens on port 8080, and stores SQLite data under `/data`. From this repository, build with `docker build -t fcg-catalog-api .` when Docker is available. The sibling `Fase 2/Orchestration` repository provides the prepared Compose entry point.

The Docker Hub Personal workflow in `.github/workflows/docker-hub.yml` runs tests before publishing the public `<namespace>/fcg-catalog-api` image. Configure GitHub variable `DOCKERHUB_USERNAME` and secret `DOCKERHUB_TOKEN` in this repository after its GitHub remote exists. See the orchestration README for image tags and pull commands.
