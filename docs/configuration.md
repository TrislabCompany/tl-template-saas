# Configuration

The settings and secrets the product reads, and how to add one. The same images run in every Environment; only these settings differ.

## 1. What the platform sets

The platform sets environment variables on each app. Locally, the AppHost sets the same names, and `.env` sets them for `docker compose` (copy it from `.env.example`).

| Setting | Apps | Set by | Locally |
| --- | --- | --- | --- |
| `PLATFORM_ENVIRONMENT` | all | The stamp: the Environment's name, such as `dev` | `local` |
| `PLATFORM_STAMP` | all | The stamp: its name, such as `shr-con-dev` | Not set |
| `DATABASE_URL` | `api`, `migrator` | The stamp's `postgres`, as a URL such as `postgresql://user:password@host:5432/db?sslmode=require` | The Postgres container |
| `REDIS_URL` | `api`, `migrator` | The stamp's `cache`, as `redis://` or `rediss://user:password@host:port` | The AppHost's Valkey; not set with Compose |
| `MESSAGING_URL` | `api`, `migrator` | The stamp's `messaging`, as `amqp://user:password@host:5672` | Not set |
| `S3_ENDPOINT`, `S3_PUBLIC_ENDPOINT`, `S3_BUCKET`, `S3_ACCESS_KEY`, `S3_SECRET_KEY` | `api`, `migrator` | The stamp's `storage`: the internal endpoint, the default hostname, the bucket `storage`, and its keys | The storage container; the public endpoint is the dashboard's address |
| `JWT_SIGNING_KEY` | `api`, `migrator` | The secret `jwt-signing-key` | A local key |
| `HANGFIRE_PASSWORD` | `api` | The secret `hangfire-password` | A local password |
| `SEED_ADMIN_PASSWORD` | `migrator` | The secret `seed-admin-password` | A local password |
| `SMTP_PASSWORD` | `api` | A secret the product adds, `smtp-password` | Not set |
| `SEED_DEMO` | `migrator`, and as `PUBLIC_DEMO_MODE` on `dashboard` | `apps[].env` in `stamp.yaml` | `true` |
| `PUBLIC_*` | `admin`, `dashboard` | `apps[].env` of the front end in `stamp.yaml` | `PUBLIC_DASHBOARD_URL` on `admin` |
| `API_UPSTREAM`, `STORAGE_UPSTREAM` | `admin`, `dashboard` | Never in a stamp | `http://api:8080` and `http://storage:9000` in `docker-compose.yml` |

Secrets arrive as ordinary environment variables too: the platform reads them from the Key Vault. The app never signs in to the Key Vault, and no secret is ever in the repository, an image or `appsettings.json`. `appsettings.Development.json` holds local values for the AppHost run only.

## 2. How the API reads them

`src/Host/App.Api/Platform/PlatformConfiguration.cs` is the one place that knows the platform's setting names. It turns them into the options the building blocks read; everything else reads those options, never an environment variable. The migrator compiles the same file. A service the stamp doesn't declare leaves its setting unset, and the building block falls back to what needs no service:

| Setting | Option | When unset |
| --- | --- | --- |
| `DATABASE_URL` | `DatabaseOptions:ConnectionString`, as an Npgsql connection string | Neither the API nor the migrator starts: Postgres is required |
| `REDIS_URL` | `CachingOptions:Redis` | An in-memory cache in each replica |
| `MESSAGING_URL` | `EventingOptions:Provider` = `RabbitMQ`, and `EventingOptions:RabbitMQ:*` | `InMemory`: events stay inside the replica, with the outbox in Postgres |
| `S3_*` | `Storage:Provider` = `s3`, and `Storage:S3:*` | `local`: files on the replica's disk, lost on restart |
| `JWT_SIGNING_KEY` | `JwtOptions:SigningKey` | The API doesn't start |
| `HANGFIRE_PASSWORD` | `HangfireOptions:Password` | No Hangfire dashboard at `/jobs`; the jobs still run |
| `SEED_ADMIN_PASSWORD` | `Seed:DefaultAdminPassword` | No first operator account, `admin@root.com` |
| `SMTP_PASSWORD` | `MailOptions:Smtp:Password` | Mail is off: each mail is logged, not sent |
| `SEED_DEMO` | `Seed:Demo` | No demo data |
| `PLATFORM_ENVIRONMENT` | `Platform:Environment`, in `GET /api/health/info` | `local` |

`SEED_DEMO` seeds the demo tenants `acme` and `globex`, and the account `superadmin@root.com`, all with the public password `Password123!`. Set it only in an Environment with no real data, such as a sandbox's `dev` or a `demo`.

## 3. `/config.json`

The browser can't read environment variables, so when a front end starts, `docker/docker-entrypoint.sh` writes `/config.json` from `docker/config.json.template`. Each `${PLATFORM_*}` and `${PUBLIC_*}` in the template becomes that setting's value, or `""` when it is unset; the script refuses a template that names any other setting. In `clients/dashboard`:

```json
{"PLATFORM_ENVIRONMENT":"dev","PLATFORM_STAMP":"shr-con-dev","PUBLIC_DEFAULT_TENANT":"","PUBLIC_DEMO_MODE":"true","PUBLIC_INACTIVITY_IDLE_MS":"","PUBLIC_INACTIVITY_WARNING_MS":""}
```

`src/env.ts` loads it once, before the app renders, and gives each unset value its default. Anything in it is public: never give a secret a `PUBLIC_` name. In the Vite dev server, `public/config.json` takes its place.

The API's address isn't in `/config.json`. The apps always call the API at the relative path `/api`.

## 4. The `/api` and `/storage` upstreams

nginx passes `/api` to the API and `/storage` to the storage. `docker-entrypoint.sh` writes both addresses into the nginx configuration when the container starts:

| When | `/api` | `/storage` |
| --- | --- | --- |
| `API_UPSTREAM` and `STORAGE_UPSTREAM` are set, as `docker-compose.yml` sets them | `http://api:8080` | `http://storage:9000` |
| Otherwise, in a stamp | `http://ca-${PLATFORM_STAMP}-api` | `http://ca-${PLATFORM_STAMP}-storage:9000` |
| Neither is set | The container stops with an error that names them | The same |

| Rule | `/api` | `/storage` |
| --- | --- | --- |
| Path | Passed on unchanged: the API serves its routes under `/api` | Passed on unchanged: the bucket is named `storage`, so `/storage/<key>` is the object's path-style address |
| `Host` header | The upstream's name | The page's hostname, unchanged, because it is part of a presigned URL's signature |
| Body | nginx's default size limit; answers streamed, for server-sent events and SignalR | No size limit of nginx's own; streamed both ways |

nginx resolves the names once, when it starts. In the Vite dev server, `vite.config.ts` proxies both paths the same way, to `API_UPSTREAM` and `STORAGE_UPSTREAM`, or `http://localhost:5030` and `http://localhost:9000`.

## 5. Add a setting or a secret

**A setting for the API**, such as `FEATURE_X`:

1. Add it to `apps[].env` of `api` in the stamp's `stamp.yaml`, in `tl-cloud-infrastructure` ([deployment.md](deployment.md)).
2. Add a mapping to `PlatformConfiguration.cs`, from `FEATURE_X` to an option, and read the option where it is used.
3. Add it with a local value to `.env.example`, to the `environment` of `migrator` in `docker-compose.yml`, which `api` shares, and to the API's settings in `AppHost.cs`.

**A secret for the API**, such as a key for an outside service:

1. Add its name to `apps[].secrets` of `api` in `stamp.yaml`, such as `mail-api-key`. The app gets it as `MAIL_API_KEY`. An engineer sets the value in the Key Vault through PIM; the platform team tells you when.
2. Map `MAIL_API_KEY` in `PlatformConfiguration.cs`, as above. If the app can't work without it, check it at start-up in `Program.cs`, as for `JWT_SIGNING_KEY`.
3. Add it to `.env.example` with a harmless local value, never the real one.

**A backing service**, such as a cache: the stamp adds it under `data` ([deployment.md](deployment.md#6-choosing-a-backing-service)), and the app gets its setting, such as `REDIS_URL`, which `PlatformConfiguration.cs` already maps.

**A value for the browser:** name it `PUBLIC_…`, add it to `apps[].env` of the front end in `stamp.yaml`, to `docker/config.json.template` and `public/config.json`, and to `.env.example`; then read it in `src/env.ts`. No change to the image's build is needed.
