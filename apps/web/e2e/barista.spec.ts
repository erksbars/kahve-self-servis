import { expect, test } from "@playwright/test";
import type { Page, Browser } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { ownerSession } from "./auth";
const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5187";
const password =
  process.env.DEMO_PASSWORD ??
  readFileSync(resolve(import.meta.dirname, "../../../.env"), "utf8")
    .match(/^DEMO_PASSWORD=(.+)$/m)?.[1]
    ?.trim();
async function staff(browser: Browser, blocked = false) {
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  if (blocked) await page.route("**/hubs/**", (r) => r.abort());
  await ownerSession(page);
  await expect(
    page.getByRole("heading", { name: "Sipariş panosu" }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Sipariş şubesi" })
    .selectOption({ label: "Moda" });
  return { page, context };
}
async function paidOrder(page: Page) {
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
  await page.getByRole("button", { name: "Ödemeye devam" }).click();
  await page
    .getByRole("button", { name: "Başarılı ödemeyi simüle et" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Sipariş alındı", exact: true }),
  ).toBeVisible();
  return Number(
    (await page.locator(".order-number strong").innerText()).replace("#", ""),
  );
}
test("new paid order arrives over SignalR, progresses live and completes even when intake is paused", async ({
  page,
  browser,
}) => {
  test.skip(!password, "DEMO_PASSWORD required");
  const barista = await staff(browser);
  try {
    await expect(
      barista.page.getByText("Canlı bağlantı açık", { exact: true }),
    ).toBeVisible();
    await barista.page
      .getByRole("button", { name: "Sesi aç", exact: true })
      .click();
    await expect(
      barista.page.getByRole("button", { name: "Sesi kapat", exact: true }),
    ).toHaveAttribute("aria-pressed", "true");
    let events = 0;
    page.on("websocket", (socket) =>
      socket.on("framereceived", (frame) => {
        if (String(frame.payload).includes("OrderChanged")) events++;
      }),
    );
    const number = await paidOrder(page);
    await expect(
      page.getByText("Canlı bağlantı açık", { exact: true }),
    ).toBeVisible();
    const card = barista.page.getByRole("article", {
      name: `Sipariş ${number}`,
      exact: true,
    });
    await expect(card).toBeVisible();
    await card.getByRole("button", { name: "Hazırlamaya al" }).click();
    await expect(
      page.getByRole("heading", { name: "Hazırlanıyor", exact: true }),
    ).toBeVisible();
    await card.getByRole("button", { name: "Hazır olarak işaretle" }).click();
    await expect(
      page.getByRole("heading", { name: "Kahven hazır", exact: true }),
    ).toBeVisible();
    const branch = barista.page.getByRole("article").filter({
      has: barista.page.getByRole("heading", { name: "Moda", exact: true }),
    });
    await branch
      .getByRole("button", { name: "Sipariş kabulünü durdur" })
      .click();
    try {
      await expect(branch.getByText("Sipariş kabulü durduruldu")).toBeVisible();
      await card
        .getByRole("button", { name: "Teslim edildi", exact: true })
        .click();
      await expect(
        page.getByRole("heading", { name: "Teslim edildi", exact: true }),
      ).toBeVisible();
      await expect(card).toHaveCount(0);
      expect(events).toBeGreaterThan(0);
    } finally {
      await branch.getByRole("button", { name: "Sipariş kabulünü aç" }).click();
      await expect(
        branch.getByText("Sipariş kabulü açık", { exact: true }),
      ).toBeVisible();
    }
  } finally {
    await barista.context.close();
  }
});
test("stale second barista gets a conflict and can reload the current status", async ({
  page,
  browser,
}) => {
  const first = await staff(browser);
  const second = await staff(browser);
  try {
    const number = await paidOrder(page);
    const card1 = first.page.getByRole("article", {
      name: `Sipariş ${number}`,
      exact: true,
    });
    const card2 = second.page.getByRole("article", {
      name: `Sipariş ${number}`,
      exact: true,
    });
    await expect(
      card1.getByRole("button", { name: "Hazırlamaya al" }),
    ).toBeVisible();
    await expect(
      card2.getByRole("button", { name: "Hazırlamaya al" }),
    ).toBeVisible();
    const cached = await second.context.request.get(
      "/api/staff/branches/" +
        (await second.page
          .getByRole("combobox", { name: "Sipariş şubesi" })
          .inputValue()) +
        "/orders",
    );
    const body = await cached.text();
    await second.page.route("**/api/staff/branches/*/orders", (r) =>
      r.fulfill({ status: 200, contentType: "application/json", body }),
    );
    await card1.getByRole("button", { name: "Hazırlamaya al" }).click();
    await expect(
      page.getByRole("heading", { name: "Hazırlanıyor", exact: true }),
    ).toBeVisible();
    const conflict = second.page.waitForResponse(
      (r) =>
        r.url().includes("/api/staff/orders/") &&
        r.request().method() === "PATCH",
    );
    await card2.getByRole("button", { name: "Hazırlamaya al" }).click();
    expect((await conflict).status()).toBe(409);
    await expect(second.page.getByRole("alert")).toContainText(
      "başka bir personel",
    );
    await second.page.unroute("**/api/staff/branches/*/orders");
    await second.page
      .getByRole("button", { name: "Yenile", exact: true })
      .click();
    await expect(
      card2.getByRole("button", { name: "Hazır olarak işaretle" }),
    ).toBeVisible();
  } finally {
    await first.context.close();
    await second.context.close();
  }
});
test("offline staff cannot update and reconnecting reloads a remotely changed order", async ({
  page,
  browser,
}) => {
  const barista = await staff(browser);
  try {
    const number = await paidOrder(page);
    const card = barista.page.getByRole("article", {
      name: `Sipariş ${number}`,
      exact: true,
    });
    await expect(
      card.getByRole("button", { name: "Hazırlamaya al" }),
    ).toBeVisible();
    const branchId = await barista.page
      .getByRole("combobox", { name: "Sipariş şubesi" })
      .inputValue();
    const data = await (
      await barista.context.request.get(
        `/api/staff/branches/${branchId}/orders`,
      )
    ).json();
    const order = data.find(
      (o: { publicNumber: number }) => o.publicNumber === number,
    );
    await barista.context.setOffline(true);
    await expect(
      barista.page.getByText("Bağlantı kesildi · Yeniden bağlanma bekleniyor", {
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      card.getByRole("button", { name: "Hazırlamaya al" }),
    ).toBeDisabled();
    const csrf = await (
      await barista.context.request.get("/api/session/csrf")
    ).json();
    const response = await barista.context.request.patch(
      `/api/staff/orders/${order.id}/status`,
      {
        headers: { "X-CSRF-TOKEN": csrf.token },
        data: { status: "Preparing", expectedVersion: order.version },
      },
    );
    expect(response.status()).toBe(200);
    await barista.context.setOffline(false);
    await expect(
      barista.page.getByText("Canlı bağlantı açık", { exact: true }),
    ).toBeVisible();
    await expect(
      card.getByRole("button", { name: "Hazır olarak işaretle" }),
    ).toBeVisible();
  } finally {
    await barista.context.close();
  }
});
test("blocked SignalR uses polling and switches back to live after connectivity returns", async ({
  page,
  browser,
}) => {
  await page.route("**/hubs/**", (r) => r.abort());
  const barista = await staff(browser, true);
  try {
    const number = await paidOrder(page);
    const card = barista.page.getByRole("article", {
      name: `Sipariş ${number}`,
      exact: true,
    });
    await expect(card).toBeVisible();
    await card.getByRole("button", { name: "Hazırlamaya al" }).click();
    await expect(
      page.getByRole("heading", { name: "Hazırlanıyor", exact: true }),
    ).toBeVisible();
    await page.unroute("**/hubs/**");
    await barista.page.unroute("**/hubs/**");
    await expect(
      page.getByText("Canlı bağlantı açık", { exact: true }),
    ).toBeVisible();
    await expect(
      barista.page.getByText("Canlı bağlantı açık", { exact: true }),
    ).toBeVisible();
  } finally {
    await barista.context.close();
  }
});
for (const width of [320, 390, 1440]) {
  test(`barista board fits ${width}px and permits delivery`, async ({
    page,
    browser,
  }) => {
    const barista = await staff(browser);
    try {
      await barista.page.setViewportSize({ width, height: 900 });
      const number = await paidOrder(page);
      const card = barista.page.getByRole("article", {
        name: `Sipariş ${number}`,
        exact: true,
      });
      await expect(card).toBeVisible();
      if (width < 700) {
        const tabs = barista.page.getByRole("navigation", {
          name: "Sipariş sütunu",
        });
        await tabs.getByRole("button").nth(2).click();
        await expect(card).toBeHidden();
        await tabs.getByRole("button").nth(0).click();
        await expect(card).toBeVisible();
      }
      expect(
        await barista.page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      ).toBe(true);
      await barista.page.screenshot({
        path: `test-results/barista-${width}.png`,
      });
      for (const action of [
        "Hazırlamaya al",
        "Hazır olarak işaretle",
        "Teslim edildi",
      ])
        await card.getByRole("button", { name: action, exact: true }).click();
      await expect(
        page.getByRole("heading", { name: "Teslim edildi", exact: true }),
      ).toBeVisible();
    } finally {
      await barista.context.close();
    }
  });
}
