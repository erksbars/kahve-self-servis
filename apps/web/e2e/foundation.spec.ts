import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const password =
  process.env.DEMO_PASSWORD ??
  readFileSync(resolve(import.meta.dirname, "../../../.env"), "utf8")
    .match(/^DEMO_PASSWORD=(.+)$/m)?.[1]
    ?.trim();
test("menu searches, filters and handles a missing branch", async ({
  page,
}) => {
  await page.goto("/b/demo-moda");
  await expect(page.getByRole("heading", { name: "Flat White" })).toBeVisible();
  await page
    .getByRole("button", { name: "Soğuk kahveler", exact: true })
    .click();
  await expect(page.getByRole("heading", { name: "Cold Brew" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Flat White" })).toHaveCount(
    0,
  );
  await page.getByRole("button", { name: "Tümü", exact: true }).click();
  await page.getByRole("textbox", { name: "Menüde ara" }).fill("latte");
  await expect(
    page.getByRole("heading", { name: "Latte", exact: true }),
  ).toBeVisible();
  await page.getByRole("textbox", { name: "Menüde ara" }).fill("bulunmayan");
  await expect(page.getByRole("status")).toContainText("ürün bulunamadı");
  await page.goto("/b/missing-branch");
  await expect(page.getByRole("alert")).toContainText("Şube bulunamadı");
});
test("staff can pause and reopen a branch; the customer sees the persisted state", async ({
  page,
  browser,
}) => {
  test.skip(!password, "DEMO_PASSWORD required");
  await page.goto("/staff");
  await page.getByLabel("E-posta", { exact: true }).fill("owner-a@demo.local");
  await page.getByLabel("Parola", { exact: true }).fill(password!);
  await page.getByRole("button", { name: "Oturum aç", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Merhaba, ekip." }),
  ).toBeVisible();
  const branch = page
    .getByRole("article")
    .filter({ has: page.getByRole("heading", { name: "Moda", exact: true }) });
  const customer = await browser.newPage();
  try {
    await branch
      .getByRole("button", { name: "Sipariş kabulünü durdur" })
      .click();
    await expect(branch.getByText("Sipariş kabulü durduruldu")).toBeVisible();
    await customer.goto("/b/demo-moda");
    await expect(
      customer.getByText("Sipariş kabulü kapalı", { exact: true }),
    ).toBeVisible();
  } finally {
    await branch.getByRole("button", { name: "Sipariş kabulünü aç" }).click();
    await expect(
      branch.getByText("Sipariş kabulü açık", { exact: true }),
    ).toBeVisible();
    await customer.close();
  }
  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(
    page.getByRole("button", { name: "Oturum aç", exact: true }),
  ).toBeVisible();
});
for (const width of [320, 390, 1440]) {
  test(`menu fits a ${width}px viewport`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/b/demo-moda");
    await expect(
      page.getByRole("heading", { name: "Flat White" }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/menu-${width}.png`,
      fullPage: true,
    });
  });
}
