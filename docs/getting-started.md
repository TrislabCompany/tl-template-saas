# Getting started

How to run the product on your machine, and how to run the tests.

## 1. What you need

| Tool | Version | For |
| --- | --- | --- |
| Docker, with Compose | Any current version | The backing services, the whole product with `docker compose`, and the integration tests' containers |
| .NET SDK | The version in `global.json` | The AppHost, the API, the migrator and the tests |
| Node.js | 22, with npm | The two front ends |

Docker alone is enough to run the product with `docker compose`. The SDK and Node are needed to work on the code and to run the tests. A ready development container with all three is in `.devcontainer/`.

## 2. Run everything with the AppHost

This is the everyday local run. Install the front ends' packages once, then start the AppHost:

```sh
npm --prefix clients/admin install
npm --prefix clients/dashboard install
dotnet run --project src/Host/App.AppHost
```

The AppHost (`src/Host/App.AppHost/AppHost.cs`) starts the same backing services as a dev stamp and the product on top of them, and opens the Aspire dashboard with the logs and traces of each:

| Resource | What it is | On your machine |
| --- | --- | --- |
| `postgres` | Postgres 17, with pgAdmin | `5432`; pgAdmin <http://localhost:5050> |
| `redis` | Valkey, the Redis-compatible cache, with RedisInsight | `6379`; RedisInsight <http://localhost:5540> |
| `storage` | rustfs, the S3-compatible storage of a dev stamp | `9000` |
| `app-db-migrator` | Applies the migrations, the seed data and the demo data, then exits. The API starts only once it has finished | None |
| `app-api` | The API | <https://localhost:7030>, with the API reference at `/scalar` |
| `app-admin` | The operator console, with the Vite dev server | <http://localhost:5173> |
| `app-dashboard` | The tenant app, with the Vite dev server | <http://localhost:5174> |

The AppHost sets the platform's setting names (`DATABASE_URL`, `REDIS_URL`, `S3_*`, …) with local values, as a stamp does, so the API reads its configuration the one way it does everywhere ([configuration.md](configuration.md)). The Vite dev servers pass `/api` to the API and `/storage` to the storage, as nginx does in the images.

## 3. Run everything with Docker Compose

Without the .NET SDK, run the four images instead. This builds them from their Dockerfiles, as the pipeline does, and starts them with Postgres and storage, like a dev stamp; there is no cache, so the API runs on its in-memory cache:

```sh
cp .env.example .env
docker compose up --build
```

The services are named like the apps in `platform.yaml` and the stamp's container services:

| Service | What it is | Port on your machine |
| --- | --- | --- |
| `postgres` | Postgres 17, the same image as a dev stamp | `5432` |
| `storage` | rustfs, the same image as a dev stamp | None: reach it through a front end's `/storage` |
| `migrator` | Creates the bucket `storage`, applies the migrations, the seed data and the demo data, then exits. `api` starts only once it has finished | None |
| `api` | The API, at `http://api:8080` inside Compose | None: reach it through a front end's `/api` |
| `admin` | nginx with the operator console, proxying `/api` to `api` and `/storage` to `storage` | <http://localhost:8081> |
| `dashboard` | nginx with the tenant app, the same proxies | <http://localhost:8082> |

The API answers `GET /api/health/info` without sign-in. Call it through a front end, to see that its `/api` proxy reaches the API:

```sh
curl http://localhost:8082/api/health/info
# {"name":"App.Api","version":"0.1.0","environment":"local"}
```

Stop the product with `docker compose down`. Add `-v` to delete the database and the files too; the migrator builds them again on the next start.

## 4. Sign in

Both runs seed the demo data (`SEED_DEMO=true`):

| Account | Tenant | Password | Where |
| --- | --- | --- | --- |
| `admin@root.com` | `root` | `SEED_ADMIN_PASSWORD`: `123Pa$$word!` with the AppHost, `.env` with Compose | admin |
| `admin@acme.com`, `admin@globex.com` and the other demo users | `acme`, `globex` | `Password123!` | dashboard, also through its demo sign-in |
| `superadmin@root.com` | `root` | `Password123!` | admin |

The demo password is public: it is in the dashboard's code. That is why `SEED_DEMO` is only ever true where there is no real data.

## 5. Run the tests

```sh
dotnet test src/App.slnx
```

This is the command `ci / ci` runs. Docker must run: the integration tests start their own throw-away containers.

| Tests | Where | What they check |
| --- | --- | --- |
| Architecture tests | `src/Tests/Architecture.Tests` | The module boundaries and conventions ([modules.md](modules.md)) |
| Unit tests | `src/Tests/<Module>.Tests`, `Framework.Tests`, `Caching.Tests`, `Generic.Tests` | Each module and building block without a database |
| Integration tests | `src/Tests/Integration.Tests`, `Integration.Middleware.Tests` | The API's endpoints against a real Postgres 17 and S3-compatible storage, the platform's probes, and the mapping of the platform's settings |

`ci / ci` also builds the four images, scans them with Trivy and checks `platform.yaml`. It doesn't build the front ends on their own: their images do that. Before a PR that changes a front end, build it:

```sh
npm --prefix clients/dashboard run build
npm --prefix clients/dashboard run lint
```

`clients/*/tests` hold Playwright tests against mocked API responses: `npm --prefix clients/admin run test:e2e`.

To build one image yourself, as the pipeline does:

```sh
docker build -f src/Host/App.Api/Dockerfile .
docker build -f clients/dashboard/Dockerfile clients/dashboard
```
