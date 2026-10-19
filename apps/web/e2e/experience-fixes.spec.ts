import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';

async function openCart(page: Page, slug = 'demo-moda') {
  await page.goto(`/b/${slug}`);
  await page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Flat White', exact: true }) }).getByRole('button', { name: 'Seç', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: /Sepete ekle/ }).click();
  await page.getByRole('button', { name: /Sepeti aç/ }).click();
}
test('logo keeps the current branch and song drafts survive reload without crossing branches', async ({ page }) => {
  await openCart(page, 'demo-besiktas');
  await page.getByLabel('Bir şarkı isteğin var mı?', { exact: false }).fill('Scorpions');
  await page.reload();
  await page.getByRole('button', { name: /Sepeti aç/ }).click();
  await expect(page.getByLabel('Bir şarkı isteğin var mı?', { exact: false })).toHaveValue('Scorpions');
  await page.getByRole('button', { name: 'Pencereyi kapat' }).click();
  await page.getByRole('link', { name: 'Şube menüsüne dön', exact: true }).click();
  await expect(page).toHaveURL(/\/b\/demo-besiktas$/);
  await openCart(page);
  await expect(page.getByLabel('Bir şarkı isteğin var mı?', { exact: false })).toHaveValue('');
});
for (const width of [320, 390, 1440]) test(`single primary checkout validates first at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 900 });
  const calls: string[] = [];
  await page.route('**/api/checkouts/quote', async route => { calls.push('quote'); await route.fulfill({ json: { branchSlug: 'demo-moda', total: 140, currency: 'TRY', items: [], calculatedAt: new Date().toISOString() } }); });
  await page.route('**/api/checkouts', async route => { calls.push('checkout'); await route.fulfill({ json: { paymentUrl: '/iyzico/pay/experience-test', amount: 140, currency: 'TRY' } }); });
  await page.route('**/api/iyzico/sessions/experience-test', route => route.fulfill({ status: 404, json: {} }));
  await openCart(page);
  await page.getByRole('button', { name: 'Ödemeye devam', exact: true }).click();
  await expect(page).toHaveURL(/\/iyzico\/pay\/experience-test$/);
  expect(calls).toEqual(['quote', 'checkout']);
});
test('changed price requires explicit confirmation before creating a checkout', async ({ page }) => {
  let checkouts = 0;
  await page.route('**/api/checkouts/quote', route => route.fulfill({ json: { branchSlug: 'demo-moda', total: 155, currency: 'TRY', items: [], calculatedAt: new Date().toISOString() } }));
  await page.route('**/api/checkouts', route => { checkouts++; return route.fulfill({ json: { paymentUrl: '/iyzico/pay/confirmed', amount: 155, currency: 'TRY' } }); });
  await page.route('**/api/iyzico/sessions/confirmed', route => route.fulfill({ status: 404, json: {} }));
  await openCart(page);
  await page.getByRole('button', { name: 'Ödemeye devam', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Sepetin tutarı değişti');
  expect(checkouts).toBe(0);
  await page.getByRole('button', { name: 'Güncel tutarla ödemeye devam', exact: true }).click();
  await expect(page).toHaveURL(/\/iyzico\/pay\/confirmed$/);
  expect(checkouts).toBe(1);
});
test('tracking keeps old data with a clear warning, shows only its song and recovers', async ({ page }) => {
  await page.route('**/hubs/**', route => route.abort());
  let fail = false;
  await page.route('**/api/orders/track/**', route => fail ? route.fulfill({ status: 503, json: { message: 'Unavailable' } }) : route.fulfill({ json: { publicNumber: 118, status: 'Preparing', paymentStatus: 'Paid', total: 140, demo: false, branch: { name: 'Beşiktaş', slug: 'demo-besiktas' }, items: [], songRequest: { text: 'Scorpions — Still Loving You', status: 'Accepted' } } }));
  await page.goto('/orders/track/' + 'd'.repeat(43));
  await expect(page.getByRole('region', { name: 'Şarkı isteğin', exact: true })).toContainText('İşletme isteğini kabul etti.');
  await expect(page.getByRole('link', { name: 'Şube menüsüne dön' })).toHaveAttribute('href', '/b/demo-besiktas');
  fail = true;
  await expect(page.getByText('Sipariş durumu şu an güncellenemiyor.', { exact: false })).toBeVisible({ timeout: 10000 });
  await expect(page.getByRole('heading', { name: 'Hazırlanıyor', exact: true })).toBeVisible();
  fail = false;
  await page.getByRole('button', { name: 'Durumu yenile' }).click();
  await expect(page.getByText('Sipariş durumu şu an güncellenemiyor.', { exact: false })).toHaveCount(0);
});
