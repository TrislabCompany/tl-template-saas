# FullStackHero — Dashboard

Tenant-facing dashboard for the FullStackHero .NET Starter Kit. Shows realtime telemetry over Server-Sent Events, current-period usage vs. plan limits (Recharts), and billing history.

Built with React 19, Vite 7, TypeScript, TanStack Query, React Router, Tailwind 4 + shadcn/ui, and Recharts. Standalone — not part of a pnpm workspace — so it plugs into .NET Aspire as a plain `ExecutableResource`.

## Prerequisites

- Node.js 20+
- The API running (locally or remote)

## Install & run

Two options — pick whichever matches how you want to develop.

### Option A — run everything through Aspire (recommended)

The AppHost launches Postgres, Valkey, rustfs storage, the API, the admin app, **and** this dashboard together, with `API_UPSTREAM` wired via service discovery.

```bash
npm install --prefix clients/dashboard   # one-time
dotnet run --project src/Host/App.AppHost
```

Aspire dashboard exposes `fsh-dashboard` on <http://localhost:5174>.

### Option B — run the frontend standalone

Useful when the API is already running elsewhere.

```bash
cd clients/dashboard
npm install
npm run dev          # http://localhost:5174
```

The dev server proxies `/api`, `/openapi`, and `/scalar` to `API_UPSTREAM` (default `http://localhost:5030`), and `/storage` to `STORAGE_UPSTREAM` (default `http://localhost:9000`).

## Scripts

| Script            | Purpose                              |
|-------------------|--------------------------------------|
| `npm run dev`     | Vite dev server on port 5174         |
| `npm run build`   | `tsc -b` + `vite build` → `dist/`    |
| `npm run preview` | Preview the production build         |
| `npm run lint`    | ESLint (flat config)                 |

## Configuration

The dev server proxies `/api` to `API_UPSTREAM` and `/storage` to `STORAGE_UPSTREAM`, as nginx does in the image.

At runtime the app reads `/config.json`, loaded by `src/env.ts` before React mounts. In the image,
`docker/docker-entrypoint.sh` writes it at container start from `docker/config.json.template`, with
the `PLATFORM_*` and `PUBLIC_*` settings only; in dev, Vite serves `public/config.json`. Never give a
secret a `PUBLIC_` name. The settings:

| Setting | Purpose |
|---|---|
| `PUBLIC_DEFAULT_TENANT` | Default tenant for unauthenticated calls (`root` when unset) |
| `PUBLIC_DEMO_MODE` | `true` shows the demo sign-in; set it where `SEED_DEMO` is true |
| `PUBLIC_INACTIVITY_IDLE_MS`, `PUBLIC_INACTIVITY_WARNING_MS` | Idle sign-out timing |

To add one, add it to `docker/config.json.template` and `public/config.json`, and read it in `src/env.ts`.

## Architecture

```
src/
├── api/                  # Typed API clients (billing, usage, subscription)
├── auth/                 # JWT-backed auth (own localStorage prefix: fsh.dashboard.*)
├── components/
│   ├── layout/           # Sidebar, Topbar, AppShell
│   ├── sse/              # SseStatusBadge, LiveFeed
│   └── ui/               # shadcn primitives
├── lib/                  # api-client, query-client, cn
├── pages/                # Overview, Activity, Invoices, Login, NotFound
├── sse/
│   ├── sse-api.ts        # POST /api/v1/sse/token
│   └── sse-context.tsx   # SSE connection manager (fetch-based streaming)
├── styles/globals.css    # Tailwind 4 CSS-first + shadcn variables
├── App.tsx, main.tsx, routes.tsx
```

### Server-Sent Events

EventSource can't send an `Authorization` header, so the flow is:

1. `POST /api/v1/sse/token` (authenticated) — returns a short-lived opaque token.
2. `GET /api/v1/sse/stream?token=<guid>` (anonymous, token-gated) — holds a long-lived `text/event-stream` response.

This app uses **fetch streaming** (not the native `EventSource` API) so it can:

- Mint a fresh single-use token on every (re)connect without relying on the browser's auto-reconnect, which would replay the already-consumed token and get 401'd.
- Apply the tenant header to the stream request.
- Use an explicit exponential backoff (1s → 30s).

The `SseProvider` in `src/sse/sse-context.tsx` is mounted inside `AppShell`, so the stream is active only when authenticated. A bounded ring buffer (200 events) is exposed via `useSse()` and consumed by `LiveFeed` and `SseStatusBadge`.

### What the overview shows

- **Usage this period** — current-month `UsageSnapshots` rendered as a bar chart of used vs. plan limit. Overage bars turn red.
- **Subscription** — plan key, status, and validity window from `GET /billing/subscriptions/me`.
- **Live activity** — rolling feed of SSE events as the backend publishes them.

## Authentication flow

Identical to the admin app: JWT in `localStorage`, `Authorization: Bearer` + `tenant` headers, single-flight refresh on 401 via `POST /api/v1/identity/token/refresh`. Keys are namespaced `fsh.dashboard.*` so both apps can run side-by-side without clobbering each other's session.

## Production build

The `Dockerfile` builds the image the platform deploys: `npm run build` emits `dist/`, served by
unprivileged nginx on port 8080 with `docker/nginx.conf`, which proxies `/api` to the API and
`/storage` to the storage service on the page's own origin. See `docs/architecture.md` at the
repository root.
