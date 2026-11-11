import { expect, test } from '@playwright/test';
import { login, OWNER_EMAIL, OWNER_PASSWORD } from './helpers';

const REFRESH_KEY = 'hbpos_refresh';
const ACCESS_KEY = 'hbpos_token';

test.describe('Phiên đăng nhập và refresh token', () => {
  test('API: xoay vòng token, token cũ bị từ chối, dùng lại token cũ thu hồi cả phiên @smoke', async ({ request }) => {
    test.setTimeout(60_000);
    const first = await (await request.post('/api/v1/auth/login', { data: { email: OWNER_EMAIL, password: OWNER_PASSWORD } })).json();
    expect(first.refreshToken).toBeTruthy();
    expect(first.expiresIn).toBeGreaterThan(0);

    const second = await (await request.post('/api/v1/auth/refresh', { data: { refreshToken: first.refreshToken } })).json();
    expect(second.refreshToken).not.toBe(first.refreshToken);
    expect((await request.get('/api/v1/me', { headers: { Authorization: `Bearer ${second.accessToken}` } })).status()).toBe(200);

    // Immediate replay is rejected but tolerated (two tabs): the new token still works.
    expect((await request.post('/api/v1/auth/refresh', { data: { refreshToken: first.refreshToken } })).status()).toBe(401);
    const third = await request.post('/api/v1/auth/refresh', { data: { refreshToken: second.refreshToken } });
    expect(third.status()).toBe(200);
    const thirdBody = await third.json();

    // Replay after the grace window means the old token leaked: the whole login is revoked.
    await new Promise(resolve => setTimeout(resolve, 16_000));
    expect((await request.post('/api/v1/auth/refresh', { data: { refreshToken: first.refreshToken } })).status()).toBe(401);
    expect((await request.post('/api/v1/auth/refresh', { data: { refreshToken: thirdBody.refreshToken } })).status()).toBe(401);
  });

  test('API: token rác hoặc rỗng bị từ chối', async ({ request }) => {
    for (const refreshToken of ['', 'khong-phai-token', 'x'.repeat(500)]) {
      expect((await request.post('/api/v1/auth/refresh', { data: { refreshToken } })).status()).toBe(401);
    }
  });

  test('UI: đóng tab rồi mở lại vẫn đăng nhập (access token mất, refresh token còn) @smoke', async ({ page }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    await page.evaluate(key => sessionStorage.removeItem(key), ACCESS_KEY);
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    expect(await page.evaluate(key => sessionStorage.getItem(key), ACCESS_KEY)).toBeTruthy();
  });

  test('UI: access token hết hạn giữa chừng được gia hạn âm thầm, không bị văng ra', async ({ page }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    const before = await page.evaluate(key => localStorage.getItem(key), REFRESH_KEY);
    // Simulate expiry: the server rejects the next authenticated call once, as it would for an expired access token.
    let rejected = false;
    await page.route('**/api/v1/products', route => {
      if (rejected) return route.continue();
      rejected = true;
      return route.fulfill({ status: 401, body: '' });
    });

    const refresh = page.waitForResponse(r => r.url().endsWith('/auth/refresh') && r.status() === 200);
    await page.getByRole('button', { name: 'Làm mới' }).click();
    await refresh;

    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toHaveCount(0);
    expect(rejected).toBe(true);
    expect(await page.evaluate(key => localStorage.getItem(key), REFRESH_KEY)).not.toBe(before);
  });

  test('UI: đăng xuất thu hồi refresh token trên máy chủ @smoke', async ({ page, request }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    const refreshToken = await page.evaluate(key => localStorage.getItem(key), REFRESH_KEY);
    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
    expect(await page.evaluate(key => localStorage.getItem(key), REFRESH_KEY)).toBeFalsy();
    await expect.poll(async () => (await request.post('/api/v1/auth/refresh', { data: { refreshToken } })).status()).toBe(401);
  });
});
