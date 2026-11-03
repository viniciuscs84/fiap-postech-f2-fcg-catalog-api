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

`/health/live` reports process liveness; `/health/ready` performs a bounded read of the owned catalog database. Broker or UsersAPI lookup disruption does not fail readiness while catalog data can still be accepted durably. `/health` remains a liveness alias. Internal event metrics are written to the process temporary directory (`/tmp/fcg-metrics.json` in containers) without an HTTP metrics endpoint.

## Purchase flow

`POST /api/library/me/purchases/{gameId}` requires a valid UsersAPI JWT. The user ID and recipient email come from its claims; the stored game supplies `Price`. A valid request records a pending purchase and publishes `OrderPlacedEvent` to the durable `payments.order-placed` RabbitMQ queue on the `fcg.events` direct exchange. The response is `202 Accepted`. The old direct self-service association endpoint remains absent.

The pending purchase and version 2 `OrderPlacedEvent` outbox row are saved atomically. A broker outage leaves the purchase pending; a worker retries and confirms publication. Confirmed rows remain for seven days, and unconfirmed rows remain until resolved. The event's stable `OrderId` identifies this purchase attempt. The service consumes `PaymentProcessedEvent` from `catalog.payment-processed` and applies it only to the matching attempt. An `Approved` result grants ownership once; a `Rejected` result settles without granting ownership. Processed-payment receipts remain for 30 days. Poison results move to `catalog.payment-processed.dead` after bounded retries. Game deletion is blocked while a purchase is pending or the game is owned.

Configure RabbitMQ through `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password`, and `RabbitMq__Exchange`. Supply the password from the runtime environment; do not commit it. The local defaults are `localhost:5672` and the RabbitMQ development user. The consumer retries when the broker is unavailable. Existing Wave 1 SQLite databases receive the pending-purchase table on startup.

The Phase 1 administrator association remains at `POST /api/admin/users/{userId}/games/{gameId}`. Set `UsersApi__BaseUrl` to the trusted UsersAPI root URL (for example, `http://localhost:8081/` or the internal Compose URL). CatalogAPI forwards only the administrator's bearer token to UsersAPI's protected user lookup. An unknown user returns `404`; an unavailable or unconfigured UsersAPI returns `503` without granting a game. Regular users cannot call the route.

The current publication path does not provide guaranteed delivery if the process stops between saving a pending purchase and publishing its event.

## Container preparation

`Dockerfile` publishes this service from its own repository into a .NET 8 runtime image. The image runs as the built-in non-root user, listens on port 8080, and stores SQLite data under `/data`. From this repository, build with `docker build -t fcg-catalog-api .` when Docker is available. The sibling `Fase 2/Orchestration` repository provides the prepared Compose entry point.

The Docker Hub Personal workflow in `.github/workflows/docker-hub.yml` runs tests before publishing the public `viniciuscs84/fcg-catalog-api` image. The public GitHub repository is `viniciuscs84/fiap-postech-f2-fcg-catalog-api`. Its `DOCKERHUB_USERNAME` variable and encrypted `DOCKERHUB_TOKEN` secret are configured. The image is published with `main` and full-commit `sha-...` tags. See the orchestration README for pull commands and token rotation.

## Local Kubernetes manifest

`k8s/all.yaml` owns this service's Deployment, ClusterIP Service, and SQLite persistent volume claim in namespace `fcg-phase2`. The shared ConfigMap supplies `UsersApi__BaseUrl=http://usersapi:8080/` and the RabbitMQ Service name; the shared Secret supplies JWT and broker credentials. Kind setup and apply order are documented in the sibling `Orchestration/README.md`. The Deployment runs as non-root UID 1654.
