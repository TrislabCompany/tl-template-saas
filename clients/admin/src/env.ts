// Runtime config — fetched once at boot from /config.json (written by
// docker/docker-entrypoint.sh in the image from the PLATFORM_* and PUBLIC_*
// settings, served by Vite from public/config.json in dev). One image works
// for every deploy. The API isn't in it: the app calls it at the relative path
// /api, which nginx (or Vite in dev) proxies to the API.
type RuntimeConfig = {
  defaultTenant: string;
  dashboardUrl: string;
  /** Idle time (ms) before the inactivity warning appears. Admin = sensitive operator console. */
  inactivityIdleMs: number;
  /** Warning-countdown length (ms) before auto sign-out. */
  inactivityWarningMs: number;
};

// Admin defaults: 10 minutes idle, then a 60-second warning.
const DEFAULT_INACTIVITY_IDLE_MS = 10 * 60_000;
const DEFAULT_INACTIVITY_WARNING_MS = 60_000;

/** Accept a positive finite number (or a numeric string, as /config.json holds) from config, else fall back. */
function positiveOr(value: unknown, fallback: number): number {
  const n = typeof value === "string" && value !== "" ? Number(value) : value;
  return typeof n === "number" && Number.isFinite(n) && n > 0 ? n : fallback;
}

/** A setting from /config.json; an unset one is "" there. */
function text(value: unknown): string | undefined {
  return typeof value === "string" && value !== "" ? value : undefined;
}

let cached: RuntimeConfig | null = null;

export async function loadRuntimeConfig(): Promise<void> {
  if (cached !== null) return;
  const res = await fetch("/config.json", { cache: "no-store" });
  if (!res.ok) {
    throw new Error(`Failed to load /config.json: ${res.status} ${res.statusText}`);
  }
  const cfg = (await res.json()) as Record<string, unknown>;
  cached = {
    defaultTenant: text(cfg.PUBLIC_DEFAULT_TENANT) ?? "root",
    // Dashboard origin used by the impersonation handoff. Dev default
    // mirrors clients/dashboard/package.json's vite port.
    dashboardUrl: (text(cfg.PUBLIC_DASHBOARD_URL) ?? "http://localhost:5174").replace(/\/$/, ""),
    inactivityIdleMs: positiveOr(cfg.PUBLIC_INACTIVITY_IDLE_MS, DEFAULT_INACTIVITY_IDLE_MS),
    inactivityWarningMs: positiveOr(cfg.PUBLIC_INACTIVITY_WARNING_MS, DEFAULT_INACTIVITY_WARNING_MS),
  };
}

function get(): RuntimeConfig {
  if (cached === null) {
    throw new Error(
      "Runtime config not loaded. main.tsx must await loadRuntimeConfig() before mounting React.",
    );
  }
  return cached;
}

export const env = {
  /** Always "": the API is at the relative path /api, on the page's own origin. */
  get apiBase(): string { return ""; },
  get defaultTenant(): string { return get().defaultTenant; },
  get dashboardUrl(): string { return get().dashboardUrl; },
  get inactivityIdleMs(): number { return get().inactivityIdleMs; },
  get inactivityWarningMs(): number { return get().inactivityWarningMs; },
};
