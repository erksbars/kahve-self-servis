import { test, expect } from '@playwright/test';
const item = { productId: 'p1', quantity: 1, name: 'Flat White', basePrice: 140, unitPrice: 140, lineTotal: 140, options: [] };
test('iyzico review does not retry initialize or claim paid', async ({ page }) => {
  let initialized = 0;
  await page.route('**/api/iyzico/sessions/**', async route => { if (route.request().method() === 'POST') initialized++; await route.fulfill({ json: { amount: 140, currency: 'TRY', paymentStatus: 'Pending', initializationState: 'Review', review: true, expired: false, branch: { name: 'Moda', slug: 'demo-moda' }, items: [item], trackingUrl: null, paymentUrl: null } }); });
  await page.goto('/iyzico/pay/' + 'a'.repeat(43)); await expect(page.getByRole('status')).toContainText('incelemesinde'); await expect(page.getByRole('button', { name: 'iyzico güvenli ödeme sayfasına geç' })).toHaveCount(0); expect(initialized).toBe(0);
});
for (const width of [320, 390, 1440]) test(`iyzico buyer form fits ${width}px without storing customer information`, async ({ page }) => {
  await page.setViewportSize({ width, height: 900 });
  await page.route('**/api/iyzico/sessions/**', route => route.fulfill({ json: { amount: 140, currency: 'TRY', paymentStatus: 'Pending', initializationState: 'NotStarted', review: false, expired: false, branch: { name: 'Moda', slug: 'demo-moda' }, items: [item], trackingUrl: null, paymentUrl: null } }));
  await page.goto('/iyzico/pay/' + 'a'.repeat(43)); await expect(page.getByLabel('Kimlik numarası')).toBeVisible(); await page.getByLabel('Ad', { exact: true }).fill('Test'); await page.getByLabel('Fatura adresi').fill('Test address'); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); expect(await page.evaluate(() => Object.values(sessionStorage).join(' '))).not.toContain('Test address'); await page.screenshot({ path: `test-results/iyzico-form-${width}.png`, fullPage: true });
});
test('iyzico ambiguous initialize status blocks second initialization', async ({ page }) => {
  await page.route('**/api/iyzico/sessions/**', route => route.fulfill({ json: { amount: 140, currency: 'TRY', paymentStatus: 'Pending', initializationState: 'Unknown', review: false, expired: false, branch: { name: 'Moda', slug: 'demo-moda' }, items: [], trackingUrl: null, paymentUrl: null } })); await page.goto('/iyzico/pay/' + 'a'.repeat(43)); await expect(page.getByRole('status')).toContainText('kesinleşmedi'); await expect(page.getByRole('button', { name: 'iyzico güvenli ödeme sayfasına geç' })).toHaveCount(0);
});
