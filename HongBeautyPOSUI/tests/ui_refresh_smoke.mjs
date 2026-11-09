import assert from 'node:assert/strict';

const storage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
globalThis.localStorage = storage;
globalThis.sessionStorage = storage;

await import('@angular/compiler');
const { App } = await import('../src/app/app.ts');

let failMovement = true;
let failDaily = false;
let statusChanges = 0;
const api = {
  user: () => ({ role: 'Owner', fullName: 'Chủ cửa hàng' }),
  can: () => true,
  patch: async () => { statusChanges++; },
  post: async (path, body) => {
    if (path === '/categories') return { id: 'new-category', name: body.name, parentId: null, active: true };
    if (path === '/attributes') return body.values.map((value, index) => ({ id: `new-option-${index}`, name: body.name, value }));
    throw new Error(`Unexpected POST ${path}`);
  },
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

const product = { id: 'product-1', name: 'Son môi', active: true, sku: 'SON-1', barcode: '', unit: 'cái', reorderLevel: 1, salePrice: 100000, costPrice: 50000, categoryId: null };
app.page.set('products');
app.createProduct();
assert.equal(app.overlay(), 'product');
assert.equal(app.page(), 'products');
app.closeOverlay();
assert.equal(app.overlay(), null);
app.editProduct(product);
assert.equal(app.editingProductId, product.id);
app.closeOverlay();
assert.equal(app.editingProductId, '');
const cancelled = app.setProductActive(product);
assert.match(app.confirmation()?.message ?? '', /Son môi/);
app.answerConfirmation(false);
await cancelled;
assert.equal(statusChanges, 0);
const accepted = app.setProductActive(product);
app.answerConfirmation(true);
await accepted;
assert.equal(statusChanges, 1);
console.log('PASS: form lớp phủ giữ trang hiện tại và xác nhận chặn thao tác khi hủy');

app.createProduct();
app.quickCategoryName = 'Son môi';
await app.addQuickCategory();
assert.equal(app.newProduct.categoryId, 'new-category');
app.quickAttributeName = 'Màu';
app.quickAttributeValues = 'Xanh, Đỏ';
await app.addQuickAttribute();
assert.equal(app.attributeOptions().length, 2);
assert.deepEqual(Object.values(app.selectedAttributeOptions), [true, true]);
assert.equal(app.overlay(), 'product');
console.log('PASS: thêm nhanh danh mục và giá trị thuộc tính tự chọn vào sản phẩm');
