import { expect } from "@playwright/test";
import type { Page, BrowserContext } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
const password =
  process.env.DEMO_PASSWORD ??
  readFileSync(resolve(import.meta.dirname, "../../../.env"), "utf8")
    .match(/^DEMO_PASSWORD=(.+)$/m)?.[1]
    ?.trim();
let cookies: Awaited<ReturnType<BrowserContext["cookies"]>> = [];

// Reuse an authenticated owner's cookie across isolated operation pages.
// The foundation test still exercises the actual login form and logout.
export async function ownerSession(page: Page) {
  if (cookies.length) await page.context().addCookies(cookies);
  await page.goto("/staff");
  if (!cookies.length) {
    await page
      .getByLabel("E-posta", { exact: true })
      .fill("owner-a@demo.local");
    await page.getByLabel("Parola", { exact: true }).fill(password!);
    await page.getByRole("button", { name: "Oturum aç", exact: true }).click();
  }
  await expect(
    page.getByRole("heading", { name: "Merhaba, ekip." }),
  ).toBeVisible();
  cookies = await page.context().cookies();
}
