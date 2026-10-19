import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { ownerSession } from "./auth";

const password =
  process.env.DEMO_PASSWORD ??
  readFileSync(resolve(import.meta.dirname, "../../../.env"), "utf8")
    .match(/^DEMO_PASSWORD=(.+)$/m)?.[1]
    ?.trim();
async function addFlatWhite(
  page: import("@playwright/test").Page,
  customized = false,
) {
  await page.goto("/b/demo-moda");
  await page
    .getByRole("article")
    .filter({
      has: page.getByRole("heading", { name: "Flat White", exact: true }),
    })
    .getByRole("button", { name: "Seç", exact: true })
    .click();
  const picker = page.getByRole("dialog", { name: "Flat White", exact: true });
  if (customized) {
    await picker.getByRole("radio", { name: /^Büyük/ }).check();
    await picker.getByRole("radio", { name: /^Yulaf sütü/ }).check();
    await picker.getByRole("checkbox", { name: /^Ek espresso/ }).check();
    await picker.getByRole("button", { name: "Adedi artır" }).click();
  }
  await picker.getByRole("button", { name: /Sepete ekle/ }).click();
}
test("customization, quantity and authoritative quote work; edits invalidate the quote", async ({
  page,
}) => {
  await addFlatWhite(page, true);
  await page.getByRole("button", { name: "Sepeti aç, 2 adet" }).click();
  const cart = page.getByRole("dialog", { name: "Sepetin", exact: true });
  await expect(
    cart.getByText("Büyük · Yulaf sütü · Ek espresso"),
  ).toBeVisible();
  await cart.getByRole("button", { name: "Sepeti doğrula" }).click();
  await expect(cart.getByRole("status")).toContainText(
    "Sepet doğrulandı · ₺430,00",
  );
  await cart.getByRole("spinbutton", { name: "Flat White adedi" }).fill("1");
  await expect(cart.getByRole("status")).toHaveCount(0);
  await cart.getByRole("button", { name: "Sepeti doğrula" }).click();
  await expect(cart.getByRole("status")).toContainText("₺215,00");
  await cart.getByRole("button", { name: "Flat White sepetten çıkar" }).click();
  await expect(
    cart.getByText("Sepetin boş. Menüden bir kahve seç."),
  ).toBeVisible();
  await cart.getByRole("button", { name: "Pencereyi kapat" }).click();
  await expect(page.getByRole("button", { name: /Sepeti aç/ })).toHaveCount(0);
});
test("cart survives refresh and remains isolated from other branch menus", async ({
  page,
}) => {
  await addFlatWhite(page);
  await page.reload();
  await expect(
    page.getByRole("button", { name: "Sepeti aç, 1 adet" }),
  ).toBeVisible();
  await page.goto("/b/demo-besiktas");
  await expect(page.getByRole("heading", { name: "Flat White" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Sepeti aç/ })).toHaveCount(0);
  await page.goto("/b/demo-moda");
  await expect(
    page.getByRole("button", { name: "Sepeti aç, 1 adet" }),
  ).toBeVisible();
});
test("network failure preserves the cart and allows a safe quote retry", async ({
  page,
}) => {
  await addFlatWhite(page);
  await page.getByRole("button", { name: "Sepeti aç, 1 adet" }).click();
  await page.route("**/api/checkouts/quote", (route) => route.abort());
  await page.getByRole("button", { name: "Sepeti doğrula" }).click();
  await expect(page.getByRole("alert")).toContainText("Bağlantı kurulamadı");
  await expect(
    page.getByRole("spinbutton", { name: "Flat White adedi" }),
  ).toHaveValue("1");
  await page.unroute("**/api/checkouts/quote");
  await page.getByRole("button", { name: "Sepeti doğrula" }).click();
  await expect(page.getByRole("status")).toContainText(
    "Sepet doğrulandı · ₺140,00",
  );
});
test("staff QR contains the selected branch, permits a shareable origin and downloads a PNG", async ({
  page,
}) => {
  test.skip(!password, "DEMO_PASSWORD required");
  await ownerSession(page);
  const branch = page
    .getByRole("article")
    .filter({ has: page.getByRole("heading", { name: "Moda", exact: true }) });
  await branch
    .getByRole("button", { name: "Menü QR kodu", exact: true })
    .click();
  const qr = page.getByRole("dialog", { name: "Moda · Menü QR kodu" });
  await expect(
    qr.getByRole("img", { name: "Moda menüsünün QR kodu" }),
  ).toBeVisible();
  await expect(qr.getByText("Bu yerel adres", { exact: false })).toBeVisible();
  await qr.getByLabel("Site adresi").fill("https://kahve.example");
  await expect(
    qr.getByRole("link", { name: "https://kahve.example/b/demo-moda" }),
  ).toHaveAttribute("href", "https://kahve.example/b/demo-moda");
  const downloaded = page.waitForEvent("download");
  await qr.getByRole("link", { name: "QR kodunu indir" }).click();
  const download = await downloaded;
  expect(download.suggestedFilename()).toBe("menu-demo-moda.png");
  const png = readFileSync((await download.path())!);
  expect(png.subarray(0, 8).toString("hex")).toBe("89504e470d0a1a0a");
});
for (const width of [320, 390, 1440]) {
  test(`picker and cart fit ${width}px and support keyboard dismissal`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/b/demo-moda");
    await page
      .getByRole("article")
      .filter({
        has: page.getByRole("heading", { name: "Flat White", exact: true }),
      })
      .getByRole("button", { name: "Seç", exact: true })
      .click();
    const picker = page.getByRole("dialog", {
      name: "Flat White",
      exact: true,
    });
    await expect(picker).toBeVisible();
    expect(
      await picker.evaluate((el) => el.scrollWidth <= el.clientWidth),
    ).toBe(true);
    await page.screenshot({
      path: `test-results/picker-${width}.png`,
      fullPage: true,
    });
    await picker.getByRole("button", { name: /Sepete ekle/ }).click();
    await page.getByRole("button", { name: /Sepeti aç/ }).click();
    const cart = page.getByRole("dialog", { name: "Sepetin", exact: true });
    await expect(cart).toBeVisible();
    expect(await cart.evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(
      true,
    );
    await cart.getByRole("button", { name: "Sepeti doğrula" }).click();
    await expect(cart.getByRole("status")).toContainText("Sepet doğrulandı");
    await page.screenshot({
      path: `test-results/cart-${width}.png`,
      fullPage: true,
    });
    await page.keyboard.press("Escape");
    await expect(cart).toHaveCount(0);
  });
}
