// Runtime config — fetched once at boot from /config.json. See
// clients/admin/src/env.ts for the rationale; the dashboard doesn't
// need dashboardUrl (the handoff is one-way: admin → dashboard).
// The API isn't in /config.json: the app calls it at the relative path /api.
type RuntimeConfig = {
  defaultTenant: string;
  /**
   * Surfaces the "Sign in with a demo account" picker on the login page.
   * A runtime flag (not a build-time VITE_ var) so a single build artifact
   * can be promoted across environments: PUBLIC_DEMO_MODE=true on the
   * dashboard app where SEED_DEMO is true, unset elsewhere.
   */
  demoMode: boolean;
  /** Idle time (ms) before the inactivity warning appears. */
  inactivityIdleMs: number;
  /** Warning-countdown length (ms) before auto sign-out. */
  inactivityWarningMs: number;
};

// Dashboard defaults: 20 minutes idle, then a 60-second warning.
const DEFAULT_INACTIVITY_IDLE_MS = 20 * 60_000;
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
    demoMode: text(cfg.PUBLIC_DEMO_MODE) === "true",
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
  get demoMode(): boolean { return get().demoMode; },
  get inactivityIdleMs(): number { return get().inactivityIdleMs; },
  get inactivityWarningMs(): number { return get().inactivityWarningMs; },
};
