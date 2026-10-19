import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:5187",
    channel: process.env.E2E_BROWSER_CHANNEL,
    trace: "retain-on-failure",
  },
  reporter: "list",
});
