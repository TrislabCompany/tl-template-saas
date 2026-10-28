# FullStackHero — Admin

Operator console for the FullStackHero .NET Starter Kit. Built with React 19, Vite 7, TypeScript, TanStack Query, React Router and Tailwind 4 + shadcn/ui.

This is a **standalone Vite app** — not part of a pnpm workspace — so it can be mounted into .NET Aspire as a plain `ExecutableResource` without monorepo friction.

## Prerequisites

- Node.js 20+
- The API running locally (`dotnet run --project src/Host/App.Api`, defaults to `http://localhost:5030`)

## Install & run

Two options — pick whichever matches how you want to develop.

### Option A — run everything through Aspire (recommended)

The AppHost launches Postgres, Valkey, rustfs storage, the API, **and** this Vite app together, with `API_UPSTREAM` wired via service discovery.

```bash
npm install --prefix clients/admin   # one-time
dotnet run --project src/Host/App.AppHost
```

Aspire dashboard will expose `fsh-admin` on <http://localhost:5173>.

### Option B — run the frontend standalone

Useful when the API is already running elsewhere (container, remote).

```bash
cd clients/admin
npm install
npm run dev          # http://localhost:5173
```

The dev server proxies `/api`, `/openapi`, and `/scalar` to `API_UPSTREAM` (default `http://localhost:5030`), and `/storage` to `STORAGE_UPSTREAM` (default `http://localhost:9000`), so browser requests stay same-origin.

## Scripts

| Script            | Purpose                              |
|-------------------|--------------------------------------|
| `npm run dev`     | Vite dev server on port 5173         |
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
| `PUBLIC_DASHBOARD_URL` | The dashboard's origin, for the impersonation hand-off |
| `PUBLIC_INACTIVITY_IDLE_MS`, `PUBLIC_INACTIVITY_WARNING_MS` | Idle sign-out timing |

To add one, add it to `docker/config.json.template` and `public/config.json`, and read it in `src/env.ts`.

## Structure

```
src/
├── api/                # Typed API client functions (per backend feature)
├── auth/               # Token store, JWT decode, auth context, protected route
├── components/
│   ├── layout/         # Sidebar, Topbar, AppShell
│   └── ui/             # shadcn primitives (Button, Card, Input, Label, Table)
├── lib/
│   ├── api-client.ts   # fetch wrapper: auth header, tenant header, single-flight refresh
│   ├── query-client.ts # TanStack QueryClient
│   └── cn.ts           # clsx + tailwind-merge
├── pages/              # Route-level components
├── styles/globals.css  # Tailwind 4 CSS-first + shadcn CSS variables
├── App.tsx             # Provider tree (QueryClient, Auth, Router)
├── main.tsx            # React entry
└── routes.tsx          # Route definitions
```

## Authentication flow

1. `POST /api/v1/identity/token/issue` with `{ email, password }` plus `tenant` header.
2. Access + refresh tokens are stored in `localStorage` (keys prefixed `fsh.admin.`).
3. The API client attaches `Authorization: Bearer <access>` and `tenant: <slug>` on every call.
4. On `401`, a single-flight refresh call hits `POST /api/v1/identity/token/refresh`, retries the original request, and logs the user out if the refresh fails.

## Styling

- Tailwind 4 CSS-first config lives in `src/styles/globals.css` (no `tailwind.config.ts`).
- Colors use shadcn/ui oklch CSS variables; dark mode is toggled via the `.dark` class on `<html>`.
- shadcn components follow the **new-york** style; `components.json` is present for `npx shadcn add ...`.

## Adding a new page

1. Add the API function in `src/api/<feature>.ts`.
2. Add the page component in `src/pages/<feature>/<name>.tsx`.
3. Register it in `src/routes.tsx` as a child of the `AppShell` route.
4. Add a nav entry in `src/components/layout/sidebar.tsx`.

## Production build

The `Dockerfile` builds the image the platform deploys: `npm run build` emits `dist/`, served by
unprivileged nginx on port 8080 with `docker/nginx.conf`, which proxies `/api` to the API and
`/storage` to the storage service on the page's own origin. See `docs/architecture.md` at the
repository root.
