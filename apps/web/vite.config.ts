import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5187,
    strictPort: true,
    proxy: { "/api": "http://127.0.0.1:5087", "/hubs": { target: "http://127.0.0.1:5087", ws: true } },
  },
  test: { environment: "jsdom", include: ["src/**/*.test.ts"] },
});
