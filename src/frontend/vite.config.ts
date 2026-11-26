import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

/**
 * Vite configuration. The build goes to `dist/` with relative asset paths so the
 * backend can serve it from the same origin (D-14). The dev proxy forwards only
 * `/api/*` to the local backend; the simulated API is never contacted (API-03).
 */
export default defineConfig({
  base: "./",
  plugins: [react()],
  build: { outDir: "dist", emptyOutDir: true },
  server: { proxy: { "/api": "http://localhost:8080" } },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./tests/setup.ts"],
    include: ["tests/**/*.test.{ts,tsx}"],
  },
});
