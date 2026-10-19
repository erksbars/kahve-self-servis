import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
const password =
  process.env.DEMO_PASSWORD ??
  readFileSync(resolve(import.meta.dirname, "../../../.env"), "utf8")
    .match(/^DEMO_PASSWORD=(.+)$/m)?.[1]
    ?.trim();

test("owner creates a product, overrides one branch price and archives it through the real API", async ({
  page,
}) => {
  test.skip(!password, "DEMO_PASSWORD required");
  await page.goto("/staff");
  await page.getByLabel("E-posta", { exact: true }).fill("owner-a@demo.local");
  await page.getByLabel("Parola", { exact: true }).fill(password!);
  await page.getByRole("button", { name: "Oturum aç", exact: true }).click();
  await page.getByRole("link", { name: "İşletme yönetimi" }).click();
  await expect(
    page.getByRole("heading", { name: "Günün görünümü" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Menü", exact: true }).click();
  await page.getByRole("button", { name: "Ürün ekle", exact: true }).click();
  const name = "E2E kahve " + Date.now();
  await page.getByLabel("Ürün adı").fill(name);
  await page.getByLabel("Temel fiyat (₺)").fill("100");
  await page.getByRole("button", { name: "Kaydet", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  const card = page
    .locator(".admin-product")
    .filter({ has: page.getByRole("heading", { name, exact: true }) });
  const catalog = await (await page.request.get("/api/admin/catalog")).json();
  const product = catalog.products.find(
    (p: { name: string }) => p.name === name,
  );
  const branches = await (await page.request.get("/api/admin/branches")).json();
  const branch = branches.find((b: { slug: string }) => b.slug === "demo-moda");
  try {
    await card.getByRole("button", { name: /^Moda:/ }).click();
    await page.getByLabel("Şube fiyatı (₺)").fill("120");
    await page.getByRole("button", { name: "Kaydet", exact: true }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await expect(card.getByRole("button", { name: /^Moda:/ })).toContainText(
      "120",
    );
    const csrf = await (await page.request.get("/api/session/csrf")).json();
    const quote = await page.request.post("/api/checkouts/quote", {
      headers: { "X-CSRF-TOKEN": csrf.token },
      data: {
        branchSlug: "demo-moda",
        items: [{ productId: product.id, quantity: 2, optionValueIds: [] }],
      },
    });
    expect(quote.ok()).toBe(true);
    expect((await quote.json()).total).toBe(240);
    await page.getByRole("button", { name: "Şubeler", exact: true }).click();
    await page
      .getByRole("article")
      .filter({ has: page.getByRole("heading", { name: "Moda", exact: true }) })
      .getByRole("button", { name: "Menü QR kodu", exact: true })
      .click();
    await expect(
      page.getByRole("img", { name: "Moda menüsünün QR kodu" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Pencereyi kapat" }).click();
    await page.getByRole("button", { name: "Özet", exact: true }).click();
    await page.screenshot({
      path: "test-results/admin-real.png",
      fullPage: true,
    });
  } finally {
    await page.goto("/admin");
    await page.getByRole("button", { name: "Menü", exact: true }).click();
    await card.getByRole("button", { name: "Ürünü düzenle" }).click();
    await page.getByLabel("Ürün satışta").uncheck();
    await page.getByRole("button", { name: "Kaydet", exact: true }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
  }
  const menu = await (
    await page.request.get("/api/branches/demo-moda/menu")
  ).json();
  expect(menu.products.some((p: { id: string }) => p.id === product.id)).toBe(
    false,
  );
  expect(branch.id).toBeTruthy();
});
