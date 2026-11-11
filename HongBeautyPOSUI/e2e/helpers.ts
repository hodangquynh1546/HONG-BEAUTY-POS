import { expect, type Page } from '@playwright/test';

export const OWNER_EMAIL = process.env['E2E_OWNER_EMAIL'] ?? '';
export const OWNER_PASSWORD = process.env['E2E_OWNER_PASSWORD'] ?? '';

export async function login(page: Page, email = OWNER_EMAIL, password = OWNER_PASSWORD): Promise<void> {
  await page.goto('/');
  await page.getByLabel('Email').fill(email);
  await page.locator('input[name=password]').fill(password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
}

export async function openPage(page: Page, label: string): Promise<void> {
  await page.locator('aside nav').getByRole('button', { name: label, exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: label })).toBeVisible();
}

/** Unique suffix so reruns against a reused database never collide. */
export const uid = (): string => Date.now().toString(36).toUpperCase();
