import { expect, test } from "@playwright/test";
import type { Page } from "@playwright/test";

async function checkout(page: Page) {
  await page.goto("/b/demo-moda");
  await page
    .getByRole("article")
    .filter({
      has: page.getByRole("heading", { name: "Flat White", exact: true }),
    })
    .getByRole("button", { name: "Seç", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "Flat White", exact: true })
    .getByRole("button", { name: /Sepete ekle/ })
    .click();
  await page.getByRole("button", { name: /Sepeti aç/ }).click();
  await page.getByRole("button", { name: "Sepeti doğrula" }).click();
  await expect(page.getByRole("status")).toContainText("Sepet doğrulandı");
  await page.getByRole("button", { name: "Ödemeye devam" }).click();
}
async function paid(page: Page) {
  await page
    .getByRole("button", { name: "Başarılı ödemeyi simüle et" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Sipariş alındı", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Sandbox ödemesi doğrulandı", { exact: false }),
  ).toContainText("₺140,00");
}
test("disabled online payment keeps the menu and cart usable without starting checkout (menu flag fixture)", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 900 });
  await page.route("**/api/branches/demo-moda/menu", async (route) => {
    const response = await route.fetch();
    await route.fulfill({
      response,
      json: { ...(await response.json()), checkoutEnabled: false },
    });
  });
  let checkouts = 0;
  await page.route("**/api/checkouts", async (route) => {
    checkouts++;
    await route.fulfill({ status: 404, json: {} });
  });
  await page.goto("/b/demo-moda");
  await expect(
    page
      .getByRole("status")
      .filter({ hasText: "Online ödeme şu anda kullanılamıyor." }),
  ).toBeVisible();
  await page
    .getByRole("article")
    .filter({
      has: page.getByRole("heading", { name: "Flat White", exact: true }),
    })
    .getByRole("button", { name: "Seç", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "Flat White", exact: true })
    .getByRole("button", { name: /Sepete ekle/ })
    .click();
  await page.getByRole("button", { name: /Sepeti aç/ }).click();
  await page.getByRole("button", { name: "Sepeti doğrula" }).click();
  await expect(
    page.getByRole("button", { name: "Ödemeye devam" }),
  ).toBeDisabled();
  await page.screenshot({
    path: "test-results/payment-disabled-390.png",
    fullPage: true,
  });
  await page.reload();
  await page.getByRole("button", { name: /Sepeti aç/ }).click();
  await expect(
    page
      .getByRole("dialog")
      .getByText("Online ödeme şu anda kullanılamıyor. Sepetin korunuyor."),
  ).toBeVisible();
  expect(checkouts).toBe(0);
});
test("guest payment creates a receipt with a secure tracking URL and clears the paid cart", async ({
  page,
}) => {
  await checkout(page);
  await expect(page.getByText("SANDBOX · TEST ÖDEMESİ")).toBeVisible();
  await paid(page);
  const path = new URL(page.url()).pathname;
  expect(/^\/orders\/track\/[A-Za-z0-9_-]{43}$/.test(path)).toBe(true);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Sipariş alındı", exact: true }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Menüye dön" }).click();
  await expect(page.getByRole("heading", { name: "Flat White" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Sepeti aç/ })).toHaveCount(0);
});
test("failed payment keeps the cart and permits a fresh attempt", async ({
  page,
}) => {
  await checkout(page);
  const previousPath = new URL(page.url()).pathname;
  await page
    .getByRole("button", { name: "Başarısız ödemeyi simüle et" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Test ödemesi başarısız" }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Sepetine dön" }).click();
  await page.getByRole("button", { name: /Sepeti aç/ }).click();
  await page.getByRole("button", { name: "Sepeti doğrula" }).click();
  await page.getByRole("button", { name: "Ödemeye devam" }).click();
  await expect(
    page.getByRole("button", { name: "Başarılı ödemeyi simüle et" }),
  ).toBeVisible();
  expect(new URL(page.url()).pathname !== previousPath).toBe(true);
  await paid(page);
});
test("a success query parameter cannot mark an unpaid session successful", async ({
  page,
}) => {
  await checkout(page);
  await expect(
    page.getByRole("button", { name: "Başarılı ödemeyi simüle et" }),
  ).toBeVisible();
  await page.goto(`${page.url()}?success=true`);
  await expect(
    page.getByRole("button", { name: "Başarılı ödemeyi simüle et" }),
  ).toBeVisible();
  await expect(
    page.getByText("Sandbox ödemesi doğrulandı", { exact: false }),
  ).toHaveCount(0);
});
test("lost checkout response is recovered with the same idempotency key and same session", async ({
  page,
}) => {
  let firstKey = "";
  let savedUrl = "";
  await page.route("**/api/checkouts", async (route) => {
    firstKey = route.request().headers()["idempotency-key"];
    const response = await route.fetch();
    savedUrl = (await response.json()).paymentUrl;
    await route.abort();
  });
  await checkout(page);
  await expect(page.getByRole("alert")).toContainText("Bağlantı kurulamadı");
  await page.unroute("**/api/checkouts");
  let retryKey = "";
  await page.route("**/api/checkouts", async (route) => {
    retryKey = route.request().headers()["idempotency-key"];
    await route.continue();
  });
  await page.getByRole("button", { name: "Sepeti doğrula" }).click();
  await page.getByRole("button", { name: "Ödemeye devam" }).click();
  await expect(
    page.getByRole("button", { name: "Başarılı ödemeyi simüle et" }),
  ).toBeVisible();
  expect(
    firstKey === retryKey && new URL(page.url()).pathname === savedUrl,
  ).toBe(true);
  await paid(page);
});
test("lost payment response is recovered from server state without a second payment", async ({
  page,
}) => {
  await checkout(page);
  await page.route("**/api/sandbox/sessions/*/decision", async (route) => {
    await route.fetch();
    await route.abort();
  });
  await paid(page);
});
test("an unknown tracking token never exposes an order", async ({ page }) => {
  await page.goto("/orders/track/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
  await expect(page.getByRole("alert")).toContainText("Kayıt bulunamadı");
  await expect(page.getByText("Sipariş numaran")).toHaveCount(0);
});
for (const width of [320, 390, 1440]) {
  test(`sandbox payment and receipt fit ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await checkout(page);
    await expect(
      page.getByRole("button", { name: "Başarılı ödemeyi simüle et" }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/payment-${width}.png`,
      fullPage: true,
    });
    await paid(page);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/receipt-${width}.png`,
      fullPage: true,
    });
  });
}
