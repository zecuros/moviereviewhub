# MovieReviewHub

University DevOps project using .NET 8, ASP.NET Core and PostgreSQL.

## Architecture

The YARP API Gateway forwards REST requests to five services. Each service owns a separate PostgreSQL database on the same PostgreSQL container and applies its EF Core migrations at startup.

| Service | Gateway route | Local port | Responsibility |
| --- | --- | --- | --- |
| ApiGateway | — | 5000 | Routes requests to services |
| AuthService | `/auth` | 5001 | Registration, login and JWT profile |
| MovieService | `/movies` | 5002 | Movie catalogue |
| ReviewService | `/reviews` | 5003 | Ratings and reviews |
| WatchlistService | `/watchlist` | 5004 | User watchlists |
| NotificationService | `/notifications` | 5005 | Notifications |

Direct service routes start with `/api/auth`, `/api/movie`, `/api/review`, `/api/watchlist` and `/api/notification`. Swagger is available at `/swagger` on each service port. `/health` reports that the HTTP application is running; it is not a database or broker readiness check.

ReviewService publishes `ReviewCreated` messages to the durable RabbitMQ `review-created` queue. NotificationService consumes messages, saves notifications, then acknowledges delivery. This decouples review creation from notification processing. It is a student demo, without an outbox or exactly-once delivery guarantee.

WatchlistService uses a typed HttpClient wrapped in an Rx.NET `IObservable<bool>` to check MovieService before adding a movie. The controller awaits the observable with `ToTask()`. This is reactive HTTP communication, not a message queue.

## Start locally

Install Docker with Compose and the .NET 8 SDK (for local builds/tests). From the repository root, copy `.env.example` to `.env` and set:

- `JWT_KEY`: a randomly generated value of at least 32 bytes.
- `GRAFANA_ADMIN_PASSWORD`: your local Grafana admin password.

The current development workspace already has a generated `.env`. This file is ignored by Git and excluded from Docker build contexts. Do not commit credentials. PostgreSQL (`postgres/postgres`) and RabbitMQ (`guest/guest`) retain their existing local-demo defaults. This Compose setup is for a trusted demo machine, not public hosting.

```sh
docker compose config --quiet
docker compose up -d --build
docker compose ps
dotnet build
dotnet test
```

Tests include JWT unit tests and live gateway E2E tests. Start the stack before running the full test suite. Tests create demo database records. `E2E_BASE_URL` can override the gateway URL.

`docker compose down` stops the stack while preserving named volumes. Adding `-v` deletes the database and dashboard/metrics storage. Jaeger uses in-memory storage, so traces disappear when it restarts.

To run services with `dotnet run`, keep PostgreSQL, RabbitMQ and monitoring containers running, stop the corresponding application containers, and use ports 5000–5005. Supply `Jwt__Key` to AuthService through an environment variable; `.env` is read by Compose, not automatically by .NET. Existing gateway and WatchlistService defaults use localhost service addresses.

## Observability

All six applications use the same OpenTelemetry configuration in their `Program.cs`, with their project names as `service.name`. Incoming ASP.NET Core requests and outgoing HttpClient calls are instrumented. W3C trace context follows gateway/HTTP calls and is carried in RabbitMQ headers, with explicit producer and consumer spans.

Traces go directly over OTLP/gRPC to Jaeger. Metrics go over OTLP/gRPC to a small OpenTelemetry Collector, which exposes all application metrics at `/metrics` on port 8889. Prometheus scrapes this endpoint every five seconds, distinguishing applications by `service_name`. The services do not expose individual `/metrics` endpoints: this avoids adding the prerelease OpenTelemetry ASP.NET Core Prometheus exporter. See the [OpenTelemetry exporter guidance](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md).

`Observability__TracesEndpoint` and `Observability__MetricsEndpoint` override exporter addresses. Defaults for local processes are `http://localhost:4317` and `http://localhost:4319`; Compose uses `jaeger:4317` and `otel-collector:4317`. Metrics export every five seconds in Compose (the SDK default applies outside Compose).

The HTTP duration histogram provides request counts, latency and status codes. Grafana automatically provisions a Prometheus datasource and a MovieReviewHub dashboard showing throughput, p95 latency and the percentage of HTTP 5xx responses. Error rate is zero when requests succeed; idle services can have no latency value. Each service counts its own requests, so adding gateway and backend counts would double-count user requests.

Logs are JSON on stdout, with UTC timestamps, levels, categories and activity trace/span IDs. Request logs include service, method, path, status and duration without request bodies, query strings or tokens. Background message processing logs include the correlated trace. Docker prefixes identify containers for startup/framework logs. No centralized log store is needed.

```sh
docker compose logs --tail 30 apigateway reviewservice notificationservice
```

| UI / endpoint | URL |
| --- | --- |
| Gateway movies | http://localhost:5000/movies |
| RabbitMQ management | http://localhost:15672 |
| Jaeger | http://localhost:16686 |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 (admin / password from `.env`) |
| Application metrics via Collector | http://localhost:8889/metrics |

Monitoring ports bind to localhost. Grafana stores its initial admin password in its persistent database; changing `.env` later does not reset an existing Grafana account.

## CI and deployment

`.github/workflows/ci.yml` restores and builds, runs SonarQube Cloud analysis (`zecuros` / `zecuros_moviereviewhub`), starts Compose, runs unit, E2E and observability checks, publishes service artifacts, and builds Docker images. Keep `SONAR_TOKEN` in GitHub Secrets. CI generates temporary JWT/Grafana credentials. Existing test and artifact steps are preserved.

`.github/workflows/cd.yml` is separate. It runs only after the entire CI workflow succeeds for a push to `main`, checks out that exact tested SHA, builds and deploys Compose, and checks HTTP endpoints, PostgreSQL and RabbitMQ. It does not deploy pull requests or feature branches. It rebuilds from the tested source; it is not an immutable image promotion pipeline. Sonar analysis upload is preserved; no new Sonar quality-gate blocking policy is assumed.

To enable a free persistent demo deployment:

1. Use a dedicated Linux machine or VM with Docker Compose and a GitHub Actions self-hosted runner labelled `moviereviewhub-demo`. Do not run untrusted pull requests on that machine.
2. Create a GitHub environment named `demo`. Add environment secrets `DEMO_JWT_KEY` and `DEMO_GRAFANA_ADMIN_PASSWORD`.
3. Set repository variable `ENABLE_DEMO_CD` to `true`. Without it, deployment is skipped.
4. Merge through your normal Git process. The CD workflow must be present on the default branch. A successful subsequent `main` CI run triggers deployment.

The runner must stay online and its checkout path must remain stable because Compose mounts provisioning files from it. Use a dedicated machine without another stack occupying the existing ports/container names. Services remain running after the job. There is no paid cloud dependency or invented SSH credential. For remote viewing, use an SSH tunnel for monitoring ports. Failed deployment checks fail the job; there is no automatic database rollback. To roll back, deploy a known-good revision manually after considering migration compatibility, preserving volumes.

## Demo / Project Defense

1. Start `docker compose up -d --build` and show `docker compose ps`.
2. Call `http://localhost:5000/movies` or create a movie in MovieService Swagger.
3. Run `dotnet test` to demonstrate authentication, movie CRUD, reactive validation and asynchronous notification delivery.
4. Open RabbitMQ management (`guest/guest`); show the `review-created` queue and its consumer. The queue can be empty because messages are acknowledged quickly.
5. Run `pwsh -File scripts/Verify-Observability.ps1`. It creates demo records and verifies metrics, dashboard queries, notification persistence and a trace spanning gateway, MovieService, WatchlistService, ReviewService and NotificationService.
6. Open the trace URL printed by the script in Jaeger. Expand HTTP and RabbitMQ producer/consumer spans.
7. In Prometheus, query `sum by (service_name) (rate(http_server_request_duration_seconds_count[1m]))`. Open the MovieReviewHub dashboard in Grafana. Repeat requests to produce traffic and allow a few scrape intervals.
8. Show correlated JSON logs with `docker compose logs`, then the GitHub Actions CI run and SonarQube Cloud analysis. Explain the separate opt-in CD workflow and its tested-commit gate.

## Coverage and shared observability

All six applications use `shared/MovieReviewHub.Observability` for their existing
OpenTelemetry registration, JSON request logging and `/health` response. The same
project owns the RabbitMQ W3C trace-header encoding and decoding. Service names,
exporter endpoints, span names and public endpoints remain unchanged.

With the Compose stack running, run the same coverage command as CI:

```powershell
dotnet build --configuration Release
dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
```

Coverlet writes `TestResults/<run-id>/coverage.opencover.xml`. CI uploads these
reports and passes their path to `sonar.cs.opencover.reportsPaths`; the scanner's
end step runs after testing. No application coverage or duplication exclusions
are configured. The Docker E2E tests validate communication, but the collector
does not instrument processes inside Docker. The in-process observability tests
exercise the shared application code and actual service startup, including HTTP instrumentation, error
logging and RabbitMQ trace propagation, to produce real application coverage.

Run `scripts/Verify-Observability.ps1` after rebuilding the stack to verify the
exported traces, metrics, dashboard queries and asynchronous notification flow.
