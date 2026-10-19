import { test, expect } from '@playwright/test';
import { resolve } from 'node:path';
import { ownerSession } from './auth';

for (const width of [320, 390, 600, 768, 1024, 1440]) test(`photo menu, product picker and cart fit ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 950 }); await page.goto('/b/demo-moda');
  const card = page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Flat White', exact: true }) });
  await expect(card.getByRole('img', { name: 'Flat White', exact: true })).toBeVisible();
  await expect.poll(() => card.locator('img').evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await card.getByRole('button', { name: 'Seç', exact: true }).click();
  const picker = page.getByRole('dialog', { name: 'Flat White', exact: true });
  await expect(picker.getByRole('img', { name: 'Flat White', exact: true })).toBeVisible();
  expect(await picker.evaluate(el => el.scrollWidth <= el.clientWidth)).toBe(true);
  await picker.getByRole('button', { name: /Sepete ekle/ }).click(); await page.getByRole('button', { name: /Sepeti aç/ }).click();
  await page.getByRole('button', { name: 'Sepeti doğrula' }).click(); await expect(page.getByRole('status')).toContainText('Sepet doğrulandı · ₺140,00');
});

test('expanded catalog has 24 photographed products and category buttons filter four bakery choices', async ({ page }) => {
  await page.goto('/b/demo-moda');
  await expect(page.getByRole('article')).toHaveCount(24);
  await expect(page.getByRole('article').getByRole('img')).toHaveCount(24);
  await page.getByRole('link', { name: 'Menüyü keşfet', exact: true }).click();
  await expect(page).toHaveURL(/#menu-title$/);
  await page.getByRole('button', { name: 'Fırından', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(4);
  await expect(page.getByRole('heading', { name: 'Bademli Kruvasan', exact: true })).toBeVisible();
  await page.getByRole('textbox', { name: 'Menüde ara' }).fill('cookie');
  await expect(page.getByRole('article')).toHaveCount(1);
});

test('new hot chocolate milk and size options use server prices', async ({ page }) => {
  await page.goto('/b/demo-moda'); await page.getByRole('textbox', { name: 'Menüde ara' }).fill('Sıcak Çikolata');
  await page.getByRole('button', { name: 'Seç', exact: true }).click();
  const picker = page.getByRole('dialog', { name: 'Sıcak Çikolata', exact: true });
  await picker.getByRole('radio', { name: /^Büyük/ }).check(); await picker.getByRole('radio', { name: /^Yulaf sütü/ }).check();
  await picker.getByRole('button', { name: /Sepete ekle/ }).click(); await page.getByRole('button', { name: /Sepeti aç/ }).click();
  await page.getByRole('button', { name: 'Sepeti doğrula' }).click(); await expect(page.getByRole('status')).toContainText('₺205,00');
});

test('dessert search opens a product without coffee options and quotes its real price', async ({ page }) => {
  await page.goto('/b/demo-moda'); await page.getByRole('textbox', { name: 'Menüde ara' }).fill('brownie');
  await expect(page.getByRole('article')).toHaveCount(1); await page.getByRole('button', { name: 'Seç', exact: true }).click();
  const picker = page.getByRole('dialog', { name: 'Brownie', exact: true }); await expect(picker.getByRole('group')).toHaveCount(0);
  await picker.getByRole('button', { name: /Sepete ekle/ }).click(); await page.getByRole('button', { name: /Sepeti aç/ }).click();
  await page.getByRole('button', { name: 'Sepeti doğrula' }).click(); await expect(page.getByRole('status')).toContainText('₺150,00');
});

test('a missing photo keeps the product selectable', async ({ page }) => {
  await page.route('**/images/menu/flat-white.webp', route => route.abort()); await page.goto('/b/demo-moda');
  const card = page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Flat White', exact: true }) });
  await expect(card.locator('.photo-fallback')).toBeVisible(); await card.getByRole('button', { name: 'Seç', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Flat White', exact: true }).getByRole('button', { name: /Sepete ekle/ })).toBeEnabled();
});

test('owner uploads a photo above 64KB then saves draft fields without a version conflict', async ({ page }) => {
  await ownerSession(page);
  const catalog = await (await page.request.get('/api/admin/catalog')).json(); const name = `Photo UI ${Date.now()}`;
  const input = { categoryId: catalog.categories[0].id, name, description: 'Photo UI test', basePrice: 100, taxRate: 0, active: true, sortOrder: 9000, optionGroups: [] };
  const csrf = await (await page.request.get('/api/session/csrf')).json();
  const create = await page.request.post('/api/admin/products', { headers: { 'X-CSRF-TOKEN': csrf.token }, data: input }); expect(create.ok()).toBe(true); const { id } = await create.json();
  try {
    await page.goto('/admin'); await page.getByRole('button', { name: 'Menü', exact: true }).click();
    const card = page.locator('.admin-product').filter({ has: page.getByRole('heading', { name, exact: true }) }); await card.getByRole('button', { name: 'Ürünü düzenle' }).click();
    await page.getByLabel('Ürün adı', { exact: true }).fill(name + ' changed');
    await page.getByLabel('Yeni görsel', { exact: true }).setInputFiles(resolve(import.meta.dirname, '../public/images/menu/croissant.webp'));
    await page.getByRole('button', { name: 'Görseli yükle', exact: true }).click(); await expect(page.getByRole('status')).toContainText('Görsel kaydedildi');
    await expect.poll(() => page.getByRole('dialog').locator('.product-photo img').evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
    await page.getByRole('button', { name: 'Kaydet', exact: true }).click(); await expect(page.getByRole('dialog')).toHaveCount(0);
    const updated = (await (await page.request.get('/api/admin/catalog')).json()).products.find((p: { id: string }) => p.id === id);
    expect(updated.name).toBe(name + ' changed'); expect(updated.version).toBe(3); expect(updated.imageUrl).toMatch(/^\/api\/catalog-images\//);
    await page.goto('/b/demo-moda'); await page.getByRole('textbox', { name: 'Menüde ara' }).fill(name);
    await expect(page.getByRole('article').getByRole('img')).toBeVisible();
  } finally {
    const updated = (await (await page.request.get('/api/admin/catalog')).json()).products.find((p: { id: string }) => p.id === id);
    const token = await (await page.request.get('/api/session/csrf')).json();
    await page.request.put(`/api/admin/products/${id}`, { headers: { 'X-CSRF-TOKEN': token.token }, data: { ...updated, active: false, expectedVersion: updated.version } });
  }
});
