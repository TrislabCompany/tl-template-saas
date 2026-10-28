# tl-template-saas

The template for multi-tenant SaaS products on the Trislab platform. Read this first; the detail is in [`docs/`](docs/).

## What it is

A complete SaaS starting point on .NET 10: a modular monolith with multi-tenancy, identity and the building blocks a SaaS product needs, an operator console and a tenant app in React, built on the [fullstackhero .NET starter kit](https://github.com/fullstackhero/dotnet-starter-kit) and adapted to the platform. Out of the box, operators create tenants with plans in `admin`, and each tenant's users sign in to `dashboard`, where the sample modules give them a product catalog, support tickets, chat, files and notifications.

## When to use it

Use it for a product that serves many customers from one deployment, each a tenant with its own users, plans and data.

| Your product… | Choose |
| --- | --- |
| Shows content, with no backend of its own | `tl-template-website` |
| Lets people work with data for one organisation, with its own API and database | `tl-template-webapp` |
| Serves many customers from one deployment, each a tenant with its own users, plans and data | `tl-template-saas`, this template |

## How to use it

You don't create a repository from this template yourself. The platform creates it when your product is onboarded: the onboarding PR in `tl-cloud-infrastructure` adds the repository to `catalog/repos.yaml`, named `<customer>-saas-<product>` (for example `trislab-saas-construction`), and the pipeline creates it from this template with its team, rules and `dev` Environment. Don't use GitHub's "Use this template" button.

The same PR writes the first stamp. It declares the secrets `jwt-signing-key`, `hangfire-password` and `seed-admin-password`, and the backing services `postgres` and `storage`, and gives `api` internal ingress. It has no cache yet, so the API runs on its in-memory cache until the stamp adds `cache`.

The new repository gets a copy of these files and never syncs with the template again. From then on the code, this README and `docs/` are your team's.

## Features included

What works on the first deploy, with no change:

- **Multi-tenancy:** a tenant catalog, provisioning, plans and per-tenant themes; every tenant's data is filtered by tenant in every query.
- **Identity:** sign-in with JWTs, users, roles, permissions, groups, sessions, impersonation and two-factor sign-in.
- **The modules:** Auditing, Files, Billing, Notifications and Webhooks, and the samples Catalog, Tickets and Chat, to keep or delete ([modules.md](docs/modules.md)). `Architecture.Tests` guards their boundaries.
- **The building blocks:** persistence, caching, eventing with an outbox, background jobs with Hangfire, mail, file storage, quotas, rate limiting, security headers and OpenTelemetry.
- **The migrator:** before each revision of the API starts, it takes a Postgres lock, applies every module's migrations and the seed data, and the demo data where `SEED_DEMO` is true, and exits.
- `/api` and `/storage` on the front ends' own hostnames: nginx passes them to the API and to the storage, so neither needs a hostname or CORS settings.
- `/healthz` and `/readyz` on all three apps. The API is ready only while its database answers.
- `/config.json`, written when a front end starts from the `PLATFORM_*` and `PUBLIC_*` settings, so one image runs in every Environment.
- The check `ci / ci` on every PR and every push to `main`: it builds the four images, runs the unit, architecture and integration tests, scans the images with Trivy, and checks `platform.yaml`.
- Images that run as a non-root user, with every base image pinned by digest.

## Technologies

| Technology | Version |
| --- | --- |
| fullstackhero .NET starter kit | Release `10.0.0`, MIT licence ([`LICENSE`](LICENSE), which stays in every repository) |
| .NET | SDK 10.0.403 (`global.json`), runtime 10.0.14 (`aspnet:10.0.14-noble-chiseled`) |
| ASP.NET Core | 10.0.14 |
| EF Core | 10.0.14, with Npgsql's provider 10.0.3 |
| Aspire (AppHost) | 13.4.0 |
| Hangfire | 1.8.23, stored in Postgres |
| Redis-compatible cache | Optional: the stamp's `cache`; Valkey 9.1.0 in the AppHost; in-memory without it |
| S3-compatible storage | The stamp's `storage`; rustfs 1.0.3 in the AppHost and `docker-compose.yml`; AWS SDK for S3 4.0.23.4 |
| RabbitMQ | Optional: the stamp's `messaging`; RabbitMQ.Client 7.2.1; in-memory without it |
| React | 19.2.5 (`admin`), 19.2.6 (`dashboard`), with React Router 7 and TanStack Query 5 |
| Vite | 7.3.2 (`admin`), 7.3.3 (`dashboard`) |
| TypeScript | 5.9.3 |
| Node.js | 22.23.4 (`node:22.23.4-alpine`, build stage of `admin` and `dashboard` only), with npm 10.9.9 |
| nginx | 1.30.6 (`nginxinc/nginx-unprivileged:1.30.6-alpine`) |
| Postgres | 17.11 (`postgres:17.11-alpine` in `docker-compose.yml` and the AppHost; 17 in the integration tests) |
| xUnit and Testcontainers | xUnit 2.9.3; Testcontainers 4.16.0 |
| Trivy | 0.74.0, in `ci / ci` |

Every base image is pinned by digest. Renovate will keep the versions current; until it runs, the platform team bumps them.

## Repository layout

| Path | Holds |
| --- | --- |
| `src/BuildingBlocks/` | The shared framework every module uses: persistence, caching, eventing, jobs, mail, storage, quotas, web |
| `src/Modules/` | One folder per module, each with a runtime project and a `.Contracts` project ([modules.md](docs/modules.md)) |
| `src/Host/` | `App.Api` with `Platform/PlatformConfiguration.cs`; `App.DbMigrator`, the init container of `api`; `App.Migrations.PostgreSQL`; `App.AppHost` for local runs |
| `src/Tests/` | `Architecture.Tests`, a test project per module and building block, and `Integration.Tests` |
| `clients/admin/` | The operator console: React, its Dockerfile and `docker/` with nginx and `/config.json` |
| `clients/dashboard/` | The tenant app, laid out like `admin` |
| `docker-compose.yml`, `.env.example` | The four images locally, without the .NET SDK |
| `platform.yaml` | The images and the sizing of each app, within the stamp's caps |
| `.github/` | The `ci` workflow, the PR template and the code owners |
| `.agents/`, `AGENTS.md`, `CLAUDE.md` | How coding agents work in this codebase |
| `docs/` | The detail behind this README |

## First steps

1. **Run it locally:** `dotnet run --project src/Host/App.AppHost`, after `npm install` in both clients; or, without the .NET SDK, `cp .env.example .env` and `docker compose up --build`, then open <http://localhost:8082>. Before you push, run `dotnet test src/App.slnx`. [getting-started.md](docs/getting-started.md) has the accounts to sign in with.
2. **Open your first PR:** change something in `src/` or `clients/`, push a branch and open a PR. `ci / ci` must pass and a code owner must approve.
3. **Deploy, release and promote:** a merge to `main` deploys to `dev`, a tag `vX.Y.Z` releases to the `release` tier, and a promotion moves a release to production. The platform adds these workflows to the template, `deploy.yml` and `release.yml` with roadmap phase 8 and `promote.yml` with phase 10; [deployment.md](docs/deployment.md) explains how they work.
4. **Add a module:** a runtime project and a `.Contracts` project in `src/Modules/`, registered in both `Program.cs` files ([modules.md](docs/modules.md#2-add-a-module)). Delete the sample modules you don't need.
5. **Add a migration:** change the module's model, then `dotnet ef migrations add <Name>` into its folder of `App.Migrations.PostgreSQL`. Keep it backward compatible ([deployment.md](docs/deployment.md#3-migrations)).
6. **Add a setting or a secret:** add it to the stamp, then map it in `src/Host/App.Api/Platform/PlatformConfiguration.cs` ([configuration.md](docs/configuration.md#5-add-a-setting-or-a-secret)).
7. **Choose a backing service:** add `cache` or `messaging` to a stamp, or move its Postgres to Neon, in its `stamp.yaml`; the app needs no change ([deployment.md](docs/deployment.md#6-choosing-a-backing-service)).

## More

- [Getting started](docs/getting-started.md): run the product with the AppHost or Docker Compose, the services, the accounts, and the tests.
- [Architecture](docs/architecture.md): the modular monolith, the images, the apps, the migrator, the backing services and the request path.
- [Configuration](docs/configuration.md): the settings and secrets, `PlatformConfiguration.cs`, `/config.json`, the `/api` and `/storage` upstreams, and how to add a setting or a secret.
- [Deployment](docs/deployment.md): Environments, deploy, release and promote, migrations, `platform.yaml` and the caps, choosing a backing service, and how to ask for more.
- [Modules](docs/modules.md): each module, how to add or delete one, and what `Architecture.Tests` enforces.
