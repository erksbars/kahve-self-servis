import { expect, test } from "@playwright/test";
import type { Page } from "@playwright/test";
import type { AdminBranch, AdminProduct, Catalog } from "../src/adminTypes";

// UI-only verification uses explicit fixtures; integration tests use real PostgreSQL separately.
async function setup(page: Page, role = "Owner") {
  const branches: AdminBranch[] = [
    {
      id: "branch-a",
      name: "Moda",
      slug: "demo-moda",
      address: "İstanbul · Moda",
      timeZone: "Europe/Istanbul",
      isAcceptingOrders: true,
      isOpenNow: true,
      preparationCapacity: 30,
      version: 1,
      hours: [],
    },
  ];
  const product: AdminProduct = {
    id: "product-a",
    categoryId: "category-a",
    name: "Flat White",
    description: "Çift espresso, kadifemsi süt.",
    basePrice: 140,
    taxRate: 10,
    active: true,
    sortOrder: 0,
    version: 1,
    optionGroups: [],
  };
  const catalog: Catalog = {
    categories: [
      { id: "category-a", name: "Espresso bazlı", sortOrder: 0, version: 1 },
    ],
    products: [product],
    branchProducts: [
      {
        branchId: "branch-a",
        productId: "product-a",
        priceOverride: null,
        available: true,
        version: 1,
      },
    ],
    branchOptions: [],
  };
  const writes: { path: string; body: Record<string, unknown> }[] = [];
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const send = (json: unknown, status = 200) =>
      route.fulfill({ json, status });
    if (path === "/api/session/me")
      return send({ email: "owner-a@demo.local", role });
    if (path === "/api/session/csrf") return send({ token: "ui-test-csrf" });
    if (request.method() === "GET") {
      if (path === "/api/admin/catalog") return send(catalog);
      if (path === "/api/admin/branches") return send(branches);
      if (path === "/api/admin/staff")
        return send([
          {
            id: "staff-a",
            email: "owner-a@demo.local",
            role: "Owner",
            active: true,
            invited: false,
            version: 1,
            branchIds: ["branch-a"],
          },
        ]);
      if (path === "/api/admin/reports/summary")
        return send({
          paidOrders: 12,
          completedOrders: 10,
          revenue: 1850,
          cancelledOrders: 0,
          failedPayments: 1,
          averagePreparationMinutes: 4.2,
          preparationSamples: 10,
          timeZone: "Europe/Istanbul",
          currency: "TRY",
          demo: true,
        });
      if (path === "/api/invitations/test-token")
        return send({ email: "new@demo.local", role: "Barista" });
    } else {
      expect(request.headers()["x-csrf-token"]).toBe("ui-test-csrf");
      const body = request.postDataJSON();
      writes.push({ path, body });
      if (path === "/api/admin/products/product-a")
        Object.assign(product, body, { version: product.version + 1 });
      if (path === "/api/admin/staff")
        return send({ id: "new-staff", invitationUrl: "/invite/test-token" });
      return send({ id: "saved", version: 2 });
    }
    return send({ message: "Fixture route missing" }, 404);
  });
  return writes;
}
for (const width of [320, 390, 1440]) {
  test(`admin preview fits ${width}px, edits options, hours and QR`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 950 });
    const writes = await setup(page);
    await page.goto("/admin");
    await expect(page.getByText("1.850", { exact: false })).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/admin-${width}.png`,
      fullPage: true,
    });
    await page.getByRole("button", { name: "Menü", exact: true }).click();
    await page.getByRole("button", { name: "Ürünü düzenle" }).click();
    await page.getByLabel("Temel fiyat (₺)").fill("155");
    await page.getByRole("button", { name: "Grup ekle" }).click();
    await page.getByLabel("Grup adı").fill("Süt");
    await page.getByLabel("Seçenek adı").fill("Yulaf");
    await page.getByLabel("Ek ücret (₺)").fill("20");
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/admin-product-${width}.png`,
      fullPage: true,
    });
    await page.getByRole("button", { name: "Kaydet", exact: true }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    expect(writes[0].body.basePrice).toBe(155);
    expect(writes[0].body.expectedVersion).toBe(1);
    expect(writes[0].body.optionGroups).toHaveLength(1);
    await page.getByRole("button", { name: "Şubeler", exact: true }).click();
    await page.getByRole("button", { name: "Şubeyi düzenle" }).click();
    await expect(page.getByLabel("Şube bağlantı adresi")).toBeDisabled();
    await page.getByLabel("Çalışma saatlerini uygula").check();
    expect(
      await page
        .getByRole("dialog")
        .evaluate((el) => el.scrollWidth <= el.clientWidth),
    ).toBe(true);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/admin-hours-${width}.png`,
      fullPage: true,
    });
    await page.getByRole("button", { name: "Kaydet", exact: true }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    expect(writes[1].body.hours).toHaveLength(6);
    await page
      .getByRole("button", { name: "Menü QR kodu", exact: true })
      .click();
    await expect(
      page.getByRole("img", { name: "Moda menüsünün QR kodu" }),
    ).toBeVisible();
  });
}
test("admin preview invites staff and activation checks password confirmation", async ({
  page,
}) => {
  const writes = await setup(page);
  await page.goto("/admin");
  await page.getByRole("button", { name: "Personel", exact: true }).click();
  await page.getByRole("button", { name: "Personel davet et" }).click();
  await page.getByLabel("E-posta", { exact: true }).fill("new@demo.local");
  await page.getByRole("button", { name: "Kaydet", exact: true }).click();
  await expect(page.getByLabel("Personel davet bağlantısı")).toHaveValue(
    /\/invite\/test-token$/,
  );
  expect(writes[0].body.branchIds).toEqual(["branch-a"]);
  await page.goto("/invite/test-token");
  await page
    .getByLabel("Yeni parola", { exact: true })
    .fill("Ui-test-password-42");
  await page.getByLabel("Parolayı tekrar girin").fill("Different-password");
  await expect(
    page.getByRole("button", { name: "Daveti kabul et" }),
  ).toBeDisabled();
  await page.getByLabel("Parolayı tekrar girin").fill("Ui-test-password-42");
  await page.getByRole("button", { name: "Daveti kabul et" }).click();
  await expect(
    page.getByRole("heading", { name: "Hesabınız hazır." }),
  ).toBeVisible();
});
test("admin preview refuses a barista session", async ({ page }) => {
  await setup(page, "Barista");
  await page.goto("/admin");
  await expect(
    page.getByRole("heading", { name: "İşletme sahibi erişimi gerekir." }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Ürün ekle" })).toHaveCount(0);
});
