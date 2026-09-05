import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, ".", "");
  const apiProxyTarget = environment.API_PROXY_TARGET;

  if (!apiProxyTarget)
    throw new Error("API_PROXY_TARGET is required.");

  return {
    base: "/app/",
    plugins: [react()],
    build: {
      outDir: "../wwwroot/app",
      emptyOutDir: true,
      sourcemap: true
    },
    server: {
      port: 5173,
      proxy: {
        "/api": {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: false
        },
        "/uploads": {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: false
        }
      }
    }
  };
});
