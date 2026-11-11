import { expect, test, type Locator } from '@playwright/test';
import { login, openPage } from './helpers';

const centreY = async (l: Locator): Promise<number> => {
  const box = await l.boundingBox();
  if (!box) throw new Error('element not visible');
  return box.y + box.height / 2;
};

test.describe('Canh hàng giao diện @smoke', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
  });

  test('nút tìm kiếm thẳng hàng với ô nhập (Đơn hàng, Bán hàng, Khách hàng)', async ({ page }) => {
    await openPage(page, 'Đơn hàng');
    const orders = page.locator('.order-filters');
    if (page.viewportSize()!.width > 650) expect(Math.abs(await centreY(orders.locator('input[name=orderSearch]')) - await centreY(orders.locator('.icon-button')))).toBeLessThanOrEqual(2);

    await openPage(page, 'Bán hàng');
    for (const row of await page.locator('.search-row').all()) {
      expect(Math.abs(await centreY(row.locator('input')) - await centreY(row.locator('.icon-button')))).toBeLessThanOrEqual(1.5);
    }

    await openPage(page, 'Khách hàng');
    const row = page.locator('.search-row').first();
    expect(Math.abs(await centreY(row.locator('input')) - await centreY(row.locator('.icon-button')))).toBeLessThanOrEqual(1.5);
  });

  test('ô lọc ngày không tràn khung ở mọi kích thước', async ({ page }) => {
    for (const name of ['Đơn hàng', 'Nhập hàng', 'Báo cáo', 'Nhật ký']) {
      await openPage(page, name);
      for (const date of await page.locator('input[type=date]:visible').all()) {
        const box = await date.boundingBox();
        const viewport = page.viewportSize()!;
        expect(box!.x).toBeGreaterThanOrEqual(0);
        expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width);
      }
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `${name} tràn ngang`).toBeLessThanOrEqual(1);
    }
  });
});
