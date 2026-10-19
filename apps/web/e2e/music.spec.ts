import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';

async function cart(page: Page) {
  await page.goto('/b/demo-moda');
  await expect(page.getByRole('button', { name: 'Sıraya bir şarkı bırak', exact: true })).toHaveCount(0);
  const product = page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Flat White', exact: true }) });
  await product.getByRole('button', { name: 'Seç', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: /Sepete ekle/ }).click();
  await page.getByRole('button', { name: /Sepeti aç/ }).click();
}
for (const width of [320, 390, 1440]) test(`optional song travels with checkout at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 950 });
  const submitted: { requestedSong: string; items: unknown[] }[] = [];
  await page.route('**/api/checkouts', async route => {
    submitted.push(route.request().postDataJSON());
    await route.fulfill({ json: { paymentUrl: '/sandbox/pay/song-test', amount: 140, currency: 'TRY', expiresAt: new Date(Date.now() + 60000).toISOString() } });
  });
  await page.route('**/api/sandbox/sessions/song-test', route => route.fulfill({ json: { amount: 140, currency: 'TRY', paymentStatus: 'Pending', expiresAt: new Date(Date.now() + 60000).toISOString(), branch: { name: 'Moda', slug: 'demo-moda' }, items: [], trackingUrl: null, demo: true } }));
  await cart(page);
  const text = 'Scorpions — Still Loving You';
  const input = page.getByLabel('Bir şarkı isteğin var mı?', { exact: false });
  await input.fill(`  ${text}  `);
  await expect(input).toHaveAttribute('maxlength', '240');
  await expect(page.getByRole('link', { name: /Spotify|YouTube/ })).toHaveCount(0);
  await page.getByRole('button', { name: 'Sepeti doğrula' }).click();
  if (width === 390) await page.getByRole('dialog').screenshot({ path: 'test-results/order-song-cart-390.png' });
  await page.getByRole('button', { name: 'Ödemeye devam' }).click();
  await expect(page).toHaveURL(/sandbox\/pay\/song-test$/);
  expect(submitted).toHaveLength(1); expect(submitted[0].requestedSong).toBe(text); expect(submitted[0].items.length).toBeGreaterThan(0);
});

test('song remains optional and survives closing the cart', async ({ page }) => {
  await cart(page);
  const input = page.getByLabel('Bir şarkı isteğin var mı?', { exact: false });
  await input.fill('Bir şarkı');
  await page.getByRole('button', { name: 'Pencereyi kapat' }).click();
  await page.getByRole('button', { name: /Sepeti aç/ }).click();
  await expect(input).toHaveValue('Bir şarkı'); await input.fill('');
  await page.getByRole('button', { name: 'Sepeti doğrula' }).click();
  await expect(page.getByRole('button', { name: 'Ödemeye devam' })).toBeEnabled();
});

async function requestPanel(page: Page, role: 'Owner' | 'Barista') {
  let status = 'Pending';
  const writes: unknown[] = [];
  await page.route('**/hubs/**', route => route.abort());
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { email: 'staff@demo.local', role } });
    if (path === '/api/session/csrf') return route.fulfill({ json: { token: 'test' } });
    if (path === '/api/admin/branches' || path === '/api/staff/branches') return route.fulfill({ json: [{ id: 'branch-1', name: 'Moda', slug: 'demo-moda', isAcceptingOrders: true, version: 1 }] });
    if (path === '/api/admin/catalog') return route.fulfill({ json: { products: [], categories: [], branchProducts: [] } });
    if (path === '/api/admin/staff') return route.fulfill({ json: [] });
    if (path === '/api/staff/branches/branch-1/song-requests') {
      const filter = new URL(route.request().url()).searchParams.get('status') ?? 'Pending';
      const items = filter === 'All' || filter === status ? [{ id: 'request-1', text: '<script>alert(1)</script> Scorpions — Still Loving You', status, version: 1, publicNumber: 118, createdAt: new Date().toISOString() }] : [];
      return route.fulfill({ json: { items, pendingCount: status === 'Pending' ? 1 : 0, total: items.length, page: 1, pageSize: 20, totalPages: 1 } });
    }
    if (path === '/api/staff/branches/branch-1/song-requests/request-1') { const body = route.request().postDataJSON(); writes.push(body); status = body.status; return route.fulfill({ json: {} }); }
    return route.fulfill({ json: [] });
  });
  await page.goto(role === 'Owner' ? '/admin' : '/staff');
  if (role === 'Owner') await page.getByRole('button', { name: 'İstek parçalar', exact: true }).click();
  return writes;
}
for (const width of [320, 390, 1440]) test(`request panel safely displays and moderates text at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 1000 });
  const writes = await requestPanel(page, 'Owner');
  const panel = page.getByRole('region', { name: 'İstek parçalar', exact: true });
  await expect(panel.getByText('#0118 · Yeni istek')).toBeVisible();
  await expect(panel.getByText('<script>alert(1)</script> Scorpions — Still Loving You')).toBeVisible();
  await expect(panel.locator('script, iframe, audio, video')).toHaveCount(0);
  await expect(page.getByText('MusicBrainz’den şarkı ekle.')).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  if (width === 390) await panel.screenshot({ path: 'test-results/order-song-panel-390.png' });
  await panel.getByRole('button', { name: 'Kabul et', exact: true }).click();
  await expect(panel.getByText('Yeni bir şarkı isteği yok.')).toBeVisible();
  await panel.getByLabel('İstek durumu', { exact: true }).selectOption('All');
  await expect(panel.getByText('#0118 · Kabul edildi')).toBeVisible();
  expect(writes).toEqual([{ status: 'Accepted', expectedVersion: 1 }]);
});
test('barista can view and reject requests from the order board', async ({ page }) => {
  const writes = await requestPanel(page, 'Barista');
  await expect(page.getByRole('link', { name: 'İstek parçalar 1 yeni' })).toBeVisible();
  const panel = page.getByRole('region', { name: 'İstek parçalar', exact: true });
  await panel.getByRole('button', { name: 'Reddet', exact: true }).click();
  await panel.getByLabel('İstek durumu', { exact: true }).selectOption('All');
  await expect(panel.getByText('#0118 · Reddedildi')).toBeVisible();
  expect(writes).toEqual([{ status: 'Rejected', expectedVersion: 1 }]);
  await expect(page.getByRole('link', { name: 'İstek parçalar 0 yeni' })).toBeVisible();
});

test('server pages and filters are navigable and return to page one on filter change', async ({ page }) => {
  await requestPanel(page, 'Owner');
  const seen: string[] = [];
  await page.route('**/api/staff/branches/branch-1/song-requests?**', async route => {
    const url = new URL(route.request().url()); const status = url.searchParams.get('status')!; const current = Number(url.searchParams.get('page'));
    seen.push(`${status}:${current}`);
    await route.fulfill({ json: { items: [{ id: `item-${current}`, text: `Sayfa ${current} parçası`, publicNumber: current, status: status === 'All' ? 'Accepted' : status, version: 1, createdAt: new Date().toISOString() }], pendingCount: 25, total: status === 'Pending' ? 25 : 145, page: current, pageSize: 20, totalPages: status === 'Pending' ? 2 : 8 } });
  });
  const panel = page.getByRole('region', { name: 'İstek parçalar', exact: true });
  await panel.getByRole('button', { name: 'İstekleri yenile' }).click();
  await expect(panel.getByText('Sayfa 1 / 2 · 25 istek')).toBeVisible();
  await panel.getByRole('button', { name: 'Sonraki', exact: true }).click();
  await expect(panel.getByText('Sayfa 2 parçası', { exact: true })).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Sonraki', exact: true })).toBeDisabled();
  await panel.getByLabel('İstek durumu', { exact: true }).selectOption('All');
  await expect(panel.getByText('Sayfa 1 / 8 · 145 istek')).toBeVisible();
  await expect(panel.getByText('25 yeni', { exact: true })).toBeVisible();
  expect(seen).toContain('Pending:2'); expect(seen).toContain('All:1');
});
