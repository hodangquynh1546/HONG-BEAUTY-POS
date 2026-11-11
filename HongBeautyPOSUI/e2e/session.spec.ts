import { expect, test, type APIRequestContext } from '@playwright/test';
import { login, OWNER_EMAIL, OWNER_PASSWORD } from './helpers';

const ACCESS_KEY = 'hbpos_token';
const WEB = { 'X-Requested-With': 'mieu-web' };
const NATIVE = { 'X-Client': 'native' };

const credentials = { email: OWNER_EMAIL, password: OWNER_PASSWORD };
const post = (request: APIRequestContext, url: string, headers: Record<string, string>, data: object = {}) =>
  request.post(`/api/v1/auth/${url}`, { headers, data });

test.describe('Phiên đăng nhập: cookie HttpOnly (web)', () => {
  test('đăng nhập đặt cookie HttpOnly + SameSite=Strict, không lộ refresh token trong JSON @smoke', async ({ request }) => {
    const response = await post(request, 'login', WEB, credentials);
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body.accessToken).toBeTruthy();
    expect(body.refreshToken ?? null).toBeNull();

    const cookie = response.headersArray().find(h => h.name.toLowerCase() === 'set-cookie')!.value;
    expect(cookie).toContain('mieu_rt=');
    expect(cookie).toMatch(/httponly/i);
    expect(cookie).toMatch(/samesite=strict/i);
    expect(cookie).toContain('path=/api/v1/auth');
  });

  test('làm mới bằng cookie xoay vòng token; thiếu header chống CSRF thì bị từ chối', async ({ request }) => {
    await post(request, 'login', WEB, credentials);
    expect((await post(request, 'refresh', {})).status()).toBe(401);           // no CSRF header
    await post(request, 'login', WEB, credentials);                            // cookie jar was cleared by the rejection

    const first = await post(request, 'refresh', WEB);
    expect(first.status()).toBe(200);
    const second = await post(request, 'refresh', WEB);
    expect(second.status()).toBe(200);
    const cookieOf = (r: typeof first) => r.headersArray().find(h => h.name.toLowerCase() === 'set-cookie')!.value.split(';')[0];
    expect(cookieOf(first)).not.toBe(cookieOf(second));   // the cookie rotates on every renewal
  });

  test('đăng xuất xóa cookie và thu hồi phiên', async ({ request }) => {
    await post(request, 'login', WEB, credentials);
    expect((await post(request, 'logout', WEB)).status()).toBe(204);
    expect((await post(request, 'refresh', WEB)).status()).toBe(401);
  });

  test('UI: refresh token không nằm trong localStorage hay document.cookie; mở lại tab vẫn đăng nhập @smoke', async ({ page, context }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();

    const stored = await page.evaluate(() => JSON.stringify({ ...localStorage }) + document.cookie);
    expect(stored).not.toContain('mieu_rt');
    expect((await context.cookies()).find(c => c.name === 'mieu_rt')?.httpOnly).toBe(true);

    await page.evaluate(key => sessionStorage.removeItem(key), ACCESS_KEY);   // like closing and reopening the tab
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
  });

  test('UI: access token hết hạn giữa chừng được gia hạn âm thầm', async ({ page }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
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
  });

  test('UI: đăng xuất xóa cookie phiên @smoke', async ({ page, context }) => {
    await login(page);
    await expect(page.getByRole('heading', { level: 1, name: 'Tổng quan' })).toBeVisible();
    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
    await expect.poll(async () => (await context.cookies()).some(c => c.name === 'mieu_rt')).toBe(false);
    await page.reload();
    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
  });
});

test.describe('Phiên đăng nhập: token trong body (app native)', () => {
  test('xoay vòng, dùng lại token cũ sau thời gian ân hạn thu hồi cả phiên', async ({ playwright, baseURL }) => {
    test.setTimeout(60_000);
    const request = await playwright.request.newContext({ baseURL });   // fresh jar: the native client sends no cookies
    const first = await (await post(request, 'login', NATIVE, credentials)).json();
    expect(first.refreshToken).toBeTruthy();

    const second = await (await post(request, 'refresh', NATIVE, { refreshToken: first.refreshToken })).json();
    expect(second.refreshToken).not.toBe(first.refreshToken);
    expect((await request.get('/api/v1/me', { headers: { Authorization: `Bearer ${second.accessToken}` } })).status()).toBe(200);

    expect((await post(request, 'refresh', NATIVE, { refreshToken: first.refreshToken })).status()).toBe(401);   // within grace
    const third = await (await post(request, 'refresh', NATIVE, { refreshToken: second.refreshToken })).json();

    await new Promise(resolve => setTimeout(resolve, 16_000));
    expect((await post(request, 'refresh', NATIVE, { refreshToken: first.refreshToken })).status()).toBe(401);  // replay
    expect((await post(request, 'refresh', NATIVE, { refreshToken: third.refreshToken })).status()).toBe(401);  // family revoked
    await request.dispose();
  });

  test('token rác hoặc rỗng bị từ chối', async ({ request }) => {
    for (const refreshToken of ['', 'khong-phai-token', 'x'.repeat(500)]) {
      expect((await post(request, 'refresh', NATIVE, { refreshToken })).status()).toBe(401);
    }
  });
});
