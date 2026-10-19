import { test, expect } from "@playwright/test";

for (const width of [320, 390, 1440]) test(`tracking receipt and progression fit ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 900 });
  let status = "Preparing";
  await page.route("**/api/orders/track/**", route => route.fulfill({ json: route.request().url().includes("/music") ? { enabled: false, canRequest: false, request: null, songs: [] } : { publicNumber: 118, status, paymentStatus: "Paid", total: 140, branch: { name: "Moda", slug: "demo-moda" }, items: [{ quantity: 1, name: "Flat White", options: [{ name: "Yulaf sütü" }], lineTotal: 140 }] } }));
  await page.goto("/orders/track/" + "d".repeat(43));
  await expect(page.getByRole("heading", { name: "Hazırlanıyor", exact: true })).toBeVisible();
  await expect(page.locator('[aria-current="step"]')).toContainText("Hazırlanıyor");
  await expect(page.getByRole("region", { name: "Sipariş özeti" })).toContainText("Yulaf sütü");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  status = "Ready";
  await page.reload();
  await expect(page.getByRole("heading", { name: "Kahven hazır" })).toBeVisible();
  await expect(page.locator('[aria-current="step"]')).toContainText("Kahven hazır");
  await expect(page.getByText("Sipariş numaranı göstererek kahveni teslim alabilirsin.", { exact: true })).toBeVisible();
});
test("unpaid tracking does not claim verified payment or preparation", async ({ page }) => {
  await page.route("**/api/orders/track/**", route => route.fulfill({ json: { publicNumber: 119, status: "PaymentPending", paymentStatus: "Pending", total: 140, branch: { name: "Moda", slug: "demo-moda" }, items: [] } }));
  await page.goto("/orders/track/" + "d".repeat(43));
  await expect(page.getByRole("heading", { name: "Ödeme bekleniyor" })).toBeVisible();
  await expect(page.getByRole("list", { name: "Sipariş aşamaları" })).toHaveCount(0);
  await expect(page.getByText("Sandbox ödemesi doğrulandı", { exact: false })).toHaveCount(0);
  await expect(page.getByRole("region", { name: "Şarkı önerisi" })).toHaveCount(0);
});
