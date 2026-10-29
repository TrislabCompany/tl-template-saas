# App.DbMigrator

A one-shot console that migrates every database to the latest migration, seeds it, and exits `0`, or `1` on failure. The API never migrates or seeds at start-up. On the platform it is the init container of the `api` app (`platform.yaml`): it runs before the API in every replica of every revision, and a failure keeps the revision from becoming ready. [docs/architecture.md](../../../docs/architecture.md#4-the-migrator) describes what it does, step by step.

It reads the same settings as the API, through `src/Host/App.Api/Platform/PlatformConfiguration.cs`, which it compiles too: `DATABASE_URL` (required), `S3_*`, `SEED_ADMIN_PASSWORD` and `SEED_DEMO` ([docs/configuration.md](../../../docs/configuration.md)).

## Verbs

```text
apply           Apply pending migrations (default). With --seed, also seed each tenant.
                Where SEED_DEMO is true, also seeds the demo tenants.
seed            Run only the seed step per tenant.
seed-demo       Seed the demo tenants (acme, globex) with users, catalog, tickets and chat.
list-pending    Print pending migrations without applying anything.

--tenant <id>   Restrict to one tenant.
--catalog-only  Skip the per-tenant pass; only the tenant catalog is migrated.
```

The image runs `apply --seed`. Locally:

```sh
DATABASE_URL=postgresql://app:app@localhost:5432/app dotnet run --project src/Host/App.DbMigrator -- apply --seed
```

## Concurrency

`PostgresMigratorLock.cs` waits for Postgres to answer, then takes a session-level advisory lock, so concurrent runs, one in each replica, serialise: the first migrates, the others wait, find nothing to do and exit. The lock is released when its connection closes, also when the process crashes.

## Demo data

`DemoSeed/DemoSeeder.cs` is idempotent: each step checks before it writes. Its accounts share the public password `Password123!` (`Seed:DemoPassword` overrides it), which the dashboard's demo sign-in shows, so `SEED_DEMO` is only ever true in an Environment with no real data.
