import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "node:path";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  // The same upstreams nginx proxies to in the image (docker/docker-entrypoint.sh).
  const apiUpstream = env.API_UPSTREAM ?? "http://localhost:5030";
  const storageUpstream = env.STORAGE_UPSTREAM ?? "http://localhost:9000";

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src"),
      },
    },
    server: {
      port: 5174,
      strictPort: true,
      proxy: {
        // ws: true forwards the WebSocket upgrade used by SignalR's hub
        // transport at /api/v1/realtime/hub. Without it the negotiate
        // succeeds over HTTP but the WS upgrade falls into Vite's own
        // dev server, so the chat status stalls on "CONNECTING" while
        // SignalR retries forever.
        "/api": { target: apiUpstream, changeOrigin: true, secure: false, ws: true },
        "/openapi": { target: apiUpstream, changeOrigin: true, secure: false },
        "/scalar": { target: apiUpstream, changeOrigin: true, secure: false },
        // Host unchanged: it is part of the presigned URL's signature.
        "/storage": { target: storageUpstream, changeOrigin: false },
      },
    },
  };
});
