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
      port: 5173,
      strictPort: true,
      proxy: {
        "/api": { target: apiUpstream, changeOrigin: true, secure: false, ws: true },
        "/openapi": { target: apiUpstream, changeOrigin: true, secure: false },
        "/scalar": { target: apiUpstream, changeOrigin: true, secure: false },
        // Host unchanged: it is part of the presigned URL's signature.
        "/storage": { target: storageUpstream, changeOrigin: false },
      },
    },
  };
});
