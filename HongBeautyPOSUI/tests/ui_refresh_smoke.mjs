import assert from 'node:assert/strict';

const storage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
globalThis.localStorage = storage;
globalThis.sessionStorage = storage;

await import('@angular/compiler');
const { App } = await import('../src/app/app.ts');

let failMovement = true;
let failDaily = false;
const api = {
  user: () => ({ role: 'Owner', fullName: 'Chủ cửa hàng' }),
  can: () => true,
  get: async path => {
    if (path.startsWith('/stock/movements')) {
      if (failMovement) throw new Error('stock movement unavailable');
      return { items: [], total: 0 };
    }
    if (path.startsWith('/customers/search')) return { items: [], total: 0 };
    if (path.startsWith('/orders/search')) return { items: [], total: 0 };
    if (path.startsWith('/reports/summary')) return { netRevenue: 123 };
    if (path.startsWith('/reports/payment-methods')) return [];
    if (path.startsWith('/reports/top-products')) return [];
    if (path.startsWith('/reports/daily')) {
      if (failDaily) throw new Error('daily report unavailable');
      return [];
    }
    if (path === '/employees') return [{ id: 'employee-1', fullName: 'Nhân viên', permissions: [] }];
    if (path === '/permissions') return ['products.read'];
    if (path === '/stocktakes') return [{ id: 'stocktake-1', items: [] }];
    return [];
  }
};

const app = new App(api);
await app.refresh();
assert.match(app.error(), /lịch sử kho/);
assert.equal(app.stocktakes().length, 1);
assert.equal(app.employees().length, 1);
assert.equal(app.report()?.netRevenue, 123);

failMovement = false;
failDaily = true;
await app.loadReport();
assert.match(app.error(), /biểu đồ/);
assert.equal(app.report()?.netRevenue, 123);
assert.deepEqual(app.dailyTrend(), []);
console.log('PASS: một API lỗi không chặn các màn khác hoặc báo cáo tổng hợp');
