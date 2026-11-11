import { expect, test } from '@playwright/test';
import { login, openPage, OWNER_EMAIL, OWNER_PASSWORD, uid } from './helpers';

test.describe('Đăng nhập @smoke', () => {
  test('từ chối mật khẩu sai, chấp nhận mật khẩu đúng, đăng xuất', async ({ page }) => {
    await login(page, OWNER_EMAIL, 'sai-mat-khau-Bang-12!');
    await expect(page.getByText('Email hoặc mật khẩu không đúng.')).toBeVisible();

    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    await expect(page.locator('aside nav button')).toHaveCount(14);

    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
  });

  test('phiên đăng nhập không tồn tại sau khi tải lại tab mới', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
    await context.close();
  });
});

test.describe.serial('Luồng kinh doanh chính', () => {
  const id = uid();
  const sku = `E2E-${id}`;
  const productName = `Son thử ${id}`;
  const supplierName = `NCC ${id}`;

  test.beforeEach(async ({ page }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
  });

  test('tạo danh mục, sản phẩm và nhà cung cấp', async ({ page }) => {
    await openPage(page, 'Danh mục');
    await page.getByLabel('Tên danh mục').fill(`Mỹ phẩm ${id}`);
    await page.getByRole('button', { name: 'Lưu danh mục' }).click();
    await expect(page.getByText('Đã tạo danh mục')).toBeVisible();

    await openPage(page, 'Sản phẩm');
    await page.getByTitle('Thêm Sản phẩm').click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('SKU').fill(sku);
    await dialog.getByLabel('Tên sản phẩm').fill(productName);
    await dialog.getByLabel('Giá bán').fill('150000');
    await dialog.getByLabel('Giá vốn').fill('90000');
    await dialog.getByRole('button', { name: /^Lưu/ }).last().click();
    await expect(page.getByText('Đã tạo sản phẩm')).toBeVisible();
    await expect(page.locator('table').getByText(productName)).toBeVisible();

    await openPage(page, 'Nhà cung cấp');
    await page.getByLabel('Mã nhà cung cấp').fill(`NCC-${id}`);
    await page.getByLabel('Tên', { exact: true }).fill(supplierName);
    await page.getByRole('button', { name: 'Lưu nhà cung cấp' }).click();
    await expect(page.getByText('Đã tạo nhà cung cấp')).toBeVisible();
  });

  test('nhập hàng: phiếu nháp chưa đổi tồn, hoàn tất thì tăng tồn', async ({ page }) => {
    await openPage(page, 'Nhập hàng');
    const form = page.locator('section[aria-label="Tạo phiếu nhập"]');
    await form.locator('select[name=supplierId]').selectOption({ label: supplierName });
    await form.locator('.purchase-line select').first().selectOption({ label: productName });
    await form.locator('.purchase-line input[type=number]').nth(0).fill('10');
    await form.locator('.purchase-line input[type=number]').nth(1).fill('90000');
    await form.getByRole('button', { name: 'Lưu phiếu nháp' }).click();
    await expect(page.getByText('Đã tạo phiếu nhập nháp')).toBeVisible();

    await openPage(page, 'Tồn kho');
    await expect(page.getByRole('row', { name: new RegExp(productName) }).first()).not.toContainText(/\b10\b/);

    await openPage(page, 'Nhập hàng');
    await page.getByRole('button', { name: 'Hoàn tất', exact: true }).first().click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Xác nhận' }).click();
    await expect(page.getByText('Đã hoàn tất phiếu nhập và cập nhật tồn kho')).toBeVisible();

    await openPage(page, 'Tồn kho');
    await expect(page.getByRole('row', { name: new RegExp(productName) }).first()).toContainText('10');
  });

  test('bán hàng 2 sản phẩm, thanh toán đủ, xem hóa đơn, trả 1 sản phẩm', async ({ page }) => {
    await openPage(page, 'Bán hàng');
    await page.locator('.product-card', { hasText: productName }).click();
    await page.locator('.product-card', { hasText: productName }).click();
    await expect(page.getByText(/300\.000/)).not.toHaveCount(0);
    await page.getByRole('button', { name: 'Xác nhận đơn' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Tạo đơn' }).click();
    await expect(page.getByText('Đã ghi nhận đơn bán hàng')).toBeVisible();

    await expect(page.getByRole('heading', { level: 1, name: 'Đơn hàng' })).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: 'Đã thanh toán' }).first();
    await row.getByRole('button', { name: 'Hóa đơn' }).click();
    await expect(page.getByRole('heading', { name: 'Hóa đơn bán hàng' })).toBeVisible();
    await expect(page.locator('#receipt-print-area')).toContainText(productName);
    await page.locator('#receipt-print-area').getByRole('button', { name: 'Đóng' }).click();

    await row.getByRole('button', { name: 'Trả hàng' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.locator('input[type=number]').first().fill('1');
    await dialog.getByLabel('Lý do trả').fill('Khách đổi ý (e2e)');
    await dialog.getByLabel('Tiền hoàn').fill('150000');
    await dialog.getByRole('button', { name: 'Xác nhận trả hàng' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Xác nhận' }).click();
    await expect(page.getByText('Đã ghi nhận trả hàng và hoàn tiền')).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: 'Đã trả một phần' })).not.toHaveCount(0);
  });

  test('thu chi, báo cáo và nhật ký phản ánh giao dịch', async ({ page }) => {
    await openPage(page, 'Thu chi');
    await page.getByRole('button', { name: /Thêm|Ghi phiếu/ }).first().click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Số tiền').fill('50000');
    await dialog.getByLabel('Nội dung').fill(`Chi e2e ${id}`);
    await dialog.getByRole('button', { name: 'Ghi nhận phiếu' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Ghi phiếu' }).click();
    await expect(page.getByText('Đã ghi nhận phiếu thu chi')).toBeVisible();
    await expect(page.getByText(`Chi e2e ${id}`)).toBeVisible();

    await openPage(page, 'Báo cáo');
    await expect(page.getByText('Doanh thu gộp')).toBeVisible();

    await page.getByRole('button', { name: 'Làm mới' }).click();
    await openPage(page, 'Nhật ký');
    await expect(page.locator('tr', { hasText: 'Ghi phiếu thu chi' }).first()).toBeVisible();
  });

  test('nhân viên chỉ thấy phần được cấp quyền', async ({ page }) => {
    const email = `nv-${id.toLowerCase()}@example.invalid`;
    const password = `Nv!${id}-Strong9x`;
    await openPage(page, 'Nhân viên');
    await page.getByLabel('Họ tên').fill('Nhân viên E2E');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Mật khẩu ban đầu').fill(password);
    await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
    await expect(page.getByText('Đã tạo nhân viên')).toBeVisible();

    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await login(page, email, password);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    const nav = page.locator('aside nav button');
    await expect(nav.filter({ hasText: 'Sản phẩm' })).toHaveCount(1);
    await expect(nav.filter({ hasText: 'Nhân viên' })).toHaveCount(0);
    await expect(nav.filter({ hasText: 'Nhật ký' })).toHaveCount(0);
    await expect(nav.filter({ hasText: 'Báo cáo' })).toHaveCount(0);
  });
});

test.describe('Giao diện di động @smoke', () => {
  test('đăng nhập và điều hướng không tràn ngang', async ({ page }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(1);
  });
});
