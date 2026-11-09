import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService, type Employee, type AuditEvent, type Product, type StockItem, type StocktakeSession, type PurchaseReceipt, type SalesOrder, type OrderLine, type OrderReceipt, type ReportSummary, type Category, type Customer, type Supplier } from './core/api.service';

type Page = 'overview' | 'products' | 'categories' | 'customers' | 'suppliers' | 'stock' | 'stocktakes' | 'purchases' | 'pos' | 'orders' | 'reports' | 'employees' | 'audit';

@Component({
  selector: 'app-root',
  imports: [FormsModule, CurrencyPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  readonly page = signal<Page>('overview');
  readonly products = signal<Product[]>([]);
  readonly posProducts = signal<Product[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly customerTotal = signal(0);
  readonly posCustomers = signal<Customer[]>([]);
  readonly suppliers = signal<Supplier[]>([]);
  readonly employees = signal<Employee[]>([]);
  readonly auditEvents = signal<AuditEvent[]>([]);
  readonly availablePermissions = signal<string[]>([]);
  readonly selectedEmployeeId = signal('');
  readonly selectedEmployee = computed(() => this.employees().find(e => e.id === this.selectedEmployeeId()) ?? null);
  readonly stock = signal<StockItem[]>([]);
  readonly stocktakes = signal<StocktakeSession[]>([]);
  readonly currentStocktake = signal<StocktakeSession | null>(null);
  readonly purchases = signal<PurchaseReceipt[]>([]);
  readonly orders = signal<SalesOrder[]>([]);
  readonly receipt = signal<OrderReceipt | null>(null);
  readonly orderTotal = signal(0);
  readonly report = signal<ReportSummary | null>(null);
  readonly returnLines = signal<OrderLine[]>([]);
  readonly cart = signal<Record<string, number>>(JSON.parse(sessionStorage.getItem('hbpos_cart') || '{}'));
  readonly cartItems = computed(() => this.products().filter(p => (this.cart()[p.id] || 0) > 0));
  readonly cartTotal = computed(() => this.cartItems().reduce((total, p) => total + p.salePrice * this.cart()[p.id], 0));
  readonly loading = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  email = '';
  password = '';
  serverUrlInput = '';
  newProduct = { sku: '', barcode: '', name: '', unit: 'cái', reorderLevel: 0, salePrice: 0, costPrice: 0, categoryId: '' };
  editingProductId = '';
  newCategory = { name: '', parentId: '' };
  editingCategoryId = '';
  newCustomer = { fullName: '', phone: '', email: '' };
  editingCustomerId = '';
  customerSearch = '';
  posCustomerSearch = '';
  posProductSearch = '';
  orderSearch = '';
  orderStatus = '';
  orderFrom = '';
  orderTo = '';
  orderSellerId = '';
  newSupplier = { code: '', name: '', phone: '', email: '' };
  editingSupplierId = '';
  newEmployee = { email: '', fullName: '', password: '' };
  resetEmployeePasswordInput = '';
  permissionSelection: Record<string, boolean> = {};
  adjustment = { productId: '', delta: 0, reason: '' };
  purchaseLines = [{ productId: '', quantity: 1, unitCost: 0 }];
  selectedSupplierId = '';
  selectedCustomerId = '';
  actualByItem: Record<string, number> = {};
  paymentMethod: 'Cash' | 'BankTransfer' | 'QrBanking' = 'Cash';
  paymentAmount = 0;
  pendingOrderId = sessionStorage.getItem('hbpos_pending_order');
  returnOrderId = '';
  returnReason = '';
  returnRefundAmount = 0;
  returnRefundMethod: 'Cash' | 'BankTransfer' | 'QrBanking' = 'Cash';
  returnQuantity: Record<string, number> = {};
  returnRestock: Record<string, boolean> = {};
  reportFrom = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
  reportTo = this.reportFrom;

  constructor(readonly api: ApiService) {}

  categoryName(id: string | null): string {
    return id ? this.categories().find(category => category.id === id)?.name || '—' : '—';
  }

  canCompleteStocktake(session: StocktakeSession): boolean {
    return session.items.length > 0 && session.items.every(item => item.actualQuantity !== null);
  }

  async ngOnInit(): Promise<void> {
    this.serverUrlInput = this.api.serverUrl();
    await this.api.restore();
    if (this.api.user()) await this.refresh();
    if (!this.pendingOrderId) this.paymentAmount = this.cartTotal();
  }

  private async loadCustomers(reset: boolean): Promise<void> {
    const skip = reset ? 0 : this.customers().length;
    const result = await this.api.get<{ items: Customer[]; total: number }>(`/customers/search?q=${encodeURIComponent(this.customerSearch.trim())}&skip=${skip}&take=50`);
    this.customers.set(reset ? result.items : [...this.customers(), ...result.items]);
    this.customerTotal.set(result.total);
  }

  async searchCustomers(reset = true): Promise<void> {
    this.loading.set(true); this.error.set('');
    try { await this.loadCustomers(reset); }
    catch { this.error.set('Không tìm được khách hàng. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  private async loadPosCustomers(): Promise<void> {
    const result = await this.api.get<{ items: Customer[] }>(`/customers/search?q=${encodeURIComponent(this.posCustomerSearch.trim())}&skip=0&take=50&activeOnly=true`);
    const selected = this.posCustomers().find(c => c.id === this.selectedCustomerId);
    this.posCustomers.set(selected && !result.items.some(c => c.id === selected.id) ? [selected, ...result.items] : result.items);
  }

  async searchPosCustomers(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try { await this.loadPosCustomers(); }
    catch { this.error.set('Không tìm được khách hàng. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  async searchPosProducts(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const found = this.posProductSearch.trim()
        ? await this.api.get<Product[]>(`/products?q=${encodeURIComponent(this.posProductSearch.trim())}`)
        : this.products();
      this.products.update(current => [...current, ...found.filter(p => !current.some(item => item.id === p.id))]);
      this.posProducts.set(found);
    } catch { this.error.set('Không tìm được sản phẩm. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  async scanPosBarcode(): Promise<void> {
    this.error.set('');
    try {
      const { CapacitorBarcodeScanner, CapacitorBarcodeScannerTypeHintALLOption } = await import('@capacitor/barcode-scanner');
      const result = await CapacitorBarcodeScanner.scanBarcode({ hint: CapacitorBarcodeScannerTypeHintALLOption.ALL });
      const barcode = result.ScanResult?.trim();
      if (!barcode) return;
      const found = await this.api.get<Product[]>(`/products?q=${encodeURIComponent(barcode)}`);
      const product = found.find(p => p.barcode === barcode && p.active && p.quantity > 0);
      if (!product) { this.error.set(`Không có sản phẩm đang bán với mã vạch ${barcode}.`); return; }
      this.products.update(current => current.some(p => p.id === product.id) ? current : [...current, product]);
      this.changeCart(product.id, 1);
    } catch { this.error.set('Không quét được mã vạch. Kiểm tra quyền camera rồi thử lại.'); }
  }

  private async loadOrders(reset: boolean): Promise<void> {
    const params = new URLSearchParams({ skip: String(reset ? 0 : this.orders().length), take: '50' });
    if (this.orderSearch.trim()) params.set('q', this.orderSearch.trim());
    if (this.orderStatus) params.set('status', this.orderStatus);
    if (this.orderFrom) params.set('from', this.orderFrom);
    if (this.orderTo) params.set('to', this.orderTo);
    if (this.orderSellerId) params.set('sellerId', this.orderSellerId);
    const result = await this.api.get<{ items: SalesOrder[]; total: number }>(`/orders/search?${params}`);
    this.orders.set(reset ? result.items : [...this.orders(), ...result.items]);
    this.orderTotal.set(result.total);
  }

  async searchOrders(reset = true): Promise<void> {
    this.loading.set(true); this.error.set('');
    try { await this.loadOrders(reset); }
    catch { this.error.set('Không tìm được đơn hàng. Vui lòng kiểm tra bộ lọc và thử lại.'); }
    finally { this.loading.set(false); }
  }

  async login(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      if (this.api.isNative) this.api.setServerUrl(this.serverUrlInput);
      await this.api.login(this.email, this.password); this.password = ''; await this.refresh();
    }
    catch (error) { this.error.set(error instanceof Error && (error.message.includes('máy chủ') || error.message.includes('HTTPS')) ? error.message : 'Không đăng nhập được. Kiểm tra máy chủ, kết nối và tài khoản.'); }
    finally { this.loading.set(false); }
  }

  async refresh(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      if (this.api.can('products.read')) {
        this.products.set(await this.api.get<Product[]>('/products'));
        this.posProducts.set(this.products());
        this.categories.set(await this.api.get<Category[]>('/categories'));
      }
      if (this.api.can('customers.read')) {
        await this.loadCustomers(true);
        await this.loadPosCustomers();
      }
      if (this.api.can('suppliers.read')) this.suppliers.set(await this.api.get<Supplier[]>('/suppliers'));
      if (this.api.can('inventory.read')) {
        this.stock.set(await this.api.get<StockItem[]>('/stock'));
        this.stocktakes.set(await this.api.get<StocktakeSession[]>('/stocktakes'));
      }
      if (this.api.can('purchases.read')) this.purchases.set(await this.api.get<PurchaseReceipt[]>('/purchases'));
      if (this.api.can('sales.read')) {
        await this.loadOrders(true);
        const pending = this.orders().find(o => o.id === this.pendingOrderId);
        if (pending) this.paymentAmount = Math.max(0, pending.total - pending.paidTotal);
      }
      if (this.api.can('reports.read')) this.report.set(await this.api.get<ReportSummary>(`/reports/summary?from=${this.reportFrom}&to=${this.reportTo}`));
      if (this.api.user()?.role === 'Owner') {
        this.employees.set(await this.api.get<Employee[]>('/employees'));
        this.availablePermissions.set(await this.api.get<string[]>('/permissions'));
        this.auditEvents.set(await this.api.get<AuditEvent[]>('/audit-events'));
      }
    } catch { this.error.set('Không tải được dữ liệu. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  editProduct(product: Product): void {
    this.editingProductId = product.id;
    this.newProduct = { sku: product.sku, barcode: product.barcode ?? '', name: product.name, unit: product.unit, reorderLevel: product.reorderLevel, salePrice: product.salePrice, costPrice: product.costPrice, categoryId: product.categoryId ?? '' };
  }

  cancelProductEdit(): void {
    this.editingProductId = '';
    this.newProduct = { sku: '', barcode: '', name: '', unit: 'cái', reorderLevel: 0, salePrice: 0, costPrice: 0, categoryId: '' };
  }

  async saveProduct(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const payload = { ...this.newProduct, categoryId: this.newProduct.categoryId || null };
      if (this.editingProductId) await this.api.put(`/products/${this.editingProductId}`, payload);
      else await this.api.post('/products', payload);
      this.notice.set(this.editingProductId ? 'Đã cập nhật sản phẩm' : 'Đã tạo sản phẩm');
      this.cancelProductEdit();
      await this.refresh();
    } catch { this.error.set('Không lưu được sản phẩm. Kiểm tra SKU, danh mục và giá trị nhập.'); }
    finally { this.loading.set(false); }
  }

  async setProductActive(product: Product): Promise<void> {
    if (!confirm(`${product.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} sản phẩm ${product.name}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.patch(`/products/${product.id}/status`, { active: !product.active });
      this.notice.set('Đã cập nhật trạng thái sản phẩm');
      await this.refresh();
    } catch { this.error.set('Không cập nhật được trạng thái sản phẩm.'); }
    finally { this.loading.set(false); }
  }

  editCategory(category: Category): void {
    this.editingCategoryId = category.id;
    this.newCategory = { name: category.name, parentId: category.parentId ?? '' };
  }

  cancelCategoryEdit(): void {
    this.editingCategoryId = '';
    this.newCategory = { name: '', parentId: '' };
  }

  async saveCategory(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const payload = { name: this.newCategory.name, parentId: this.newCategory.parentId || null };
      if (this.editingCategoryId) await this.api.put(`/categories/${this.editingCategoryId}`, payload);
      else await this.api.post('/categories', payload);
      this.notice.set(this.editingCategoryId ? 'Đã cập nhật danh mục' : 'Đã tạo danh mục');
      this.cancelCategoryEdit();
      await this.refresh();
    } catch { this.error.set('Không lưu được danh mục. Kiểm tra tên, danh mục cha và quan hệ cấp cha.'); }
    finally { this.loading.set(false); }
  }

  async setCategoryActive(category: Category): Promise<void> {
    if (!confirm(`${category.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} danh mục ${category.name}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.patch(`/categories/${category.id}/status`, { active: !category.active });
      this.notice.set('Đã cập nhật trạng thái danh mục');
      await this.refresh();
    } catch { this.error.set('Không đổi được trạng thái. Nếu có danh mục con đang dùng, hãy ngừng chúng trước.'); }
    finally { this.loading.set(false); }
  }

  editCustomer(customer: Customer): void {
    this.editingCustomerId = customer.id;
    this.newCustomer = { fullName: customer.fullName, phone: customer.phone ?? '', email: customer.email ?? '' };
  }

  cancelCustomerEdit(): void {
    this.editingCustomerId = '';
    this.newCustomer = { fullName: '', phone: '', email: '' };
  }

  async saveCustomer(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      if (this.editingCustomerId) await this.api.put(`/customers/${this.editingCustomerId}`, this.newCustomer);
      else await this.api.post('/customers', this.newCustomer);
      this.notice.set(this.editingCustomerId ? 'Đã cập nhật khách hàng' : 'Đã tạo khách hàng');
      this.cancelCustomerEdit();
      await this.refresh();
    } catch { this.error.set('Không lưu được khách hàng. Kiểm tra thông tin nhập.'); }
    finally { this.loading.set(false); }
  }

  async setCustomerActive(customer: Customer): Promise<void> {
    if (!confirm(`${customer.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} khách hàng ${customer.fullName}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.patch(`/customers/${customer.id}/status`, { active: !customer.active });
      this.notice.set('Đã cập nhật trạng thái khách hàng');
      await this.refresh();
    } catch { this.error.set('Không cập nhật được trạng thái khách hàng.'); }
    finally { this.loading.set(false); }
  }

  editSupplier(supplier: Supplier): void {
    this.editingSupplierId = supplier.id;
    this.newSupplier = { code: supplier.code, name: supplier.name, phone: supplier.phone ?? '', email: supplier.email ?? '' };
  }

  cancelSupplierEdit(): void {
    this.editingSupplierId = '';
    this.newSupplier = { code: '', name: '', phone: '', email: '' };
  }

  async saveSupplier(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      if (this.editingSupplierId) await this.api.put(`/suppliers/${this.editingSupplierId}`, this.newSupplier);
      else await this.api.post('/suppliers', this.newSupplier);
      this.notice.set(this.editingSupplierId ? 'Đã cập nhật nhà cung cấp' : 'Đã tạo nhà cung cấp');
      this.cancelSupplierEdit();
      await this.refresh();
    } catch { this.error.set('Không lưu được nhà cung cấp. Kiểm tra mã đã trùng hoặc thông tin nhập.'); }
    finally { this.loading.set(false); }
  }

  async setSupplierActive(supplier: Supplier): Promise<void> {
    if (!confirm(`${supplier.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} nhà cung cấp ${supplier.name}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.patch(`/suppliers/${supplier.id}/status`, { active: !supplier.active });
      this.notice.set('Đã cập nhật trạng thái nhà cung cấp');
      await this.refresh();
    } catch { this.error.set('Không cập nhật được trạng thái nhà cung cấp.'); }
    finally { this.loading.set(false); }
  }

  async addEmployee(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      await this.api.post('/employees', { ...this.newEmployee, permissions: ['products.read'] });
      this.newEmployee = { email: '', fullName: '', password: '' };
      this.notice.set('Đã tạo nhân viên');
      await this.refresh();
    } catch { this.error.set('Không tạo được nhân viên. Kiểm tra thông tin và email trùng.'); }
    finally { this.loading.set(false); }
  }

  selectEmployee(employee: Employee): void {
    this.selectedEmployeeId.set(employee.id);
    this.resetEmployeePasswordInput = '';
    this.permissionSelection = Object.fromEntries(this.availablePermissions().map(permission => [permission, employee.permissions.includes(permission)]));
  }

  async resetEmployeePassword(): Promise<void> {
    const employee = this.selectedEmployee();
    if (!employee || !confirm(`Đặt lại mật khẩu cho ${employee.fullName}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.put(`/employees/${employee.id}/password`, { password: this.resetEmployeePasswordInput });
      this.resetEmployeePasswordInput = '';
      this.notice.set(`Đã đặt lại mật khẩu cho ${employee.fullName}; phiên đăng nhập cũ sẽ hết hiệu lực.`);
    } catch { this.error.set('Không đổi được mật khẩu. Cần 14–128 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt.'); }
    finally { this.loading.set(false); }
  }

  permissionLabel(permission: string): string {
    const labels: Record<string, string> = {
      'products.read': 'Xem sản phẩm', 'products.write': 'Tạo sản phẩm, danh mục',
      'inventory.read': 'Xem tồn kho', 'inventory.adjust': 'Điều chỉnh và kiểm kho',
      'purchases.read': 'Xem phiếu nhập', 'purchases.write': 'Tạo và hoàn tất phiếu nhập',
      'sales.read': 'Xem đơn bán', 'sales.write': 'Bán hàng',
      'payments.write': 'Nhận thanh toán', 'returns.write': 'Trả hàng và hoàn tiền',
      'customers.read': 'Xem khách hàng', 'customers.write': 'Tạo khách hàng',
      'suppliers.read': 'Xem nhà cung cấp', 'suppliers.write': 'Tạo nhà cung cấp',
      'reports.read': 'Xem báo cáo'
    };
    return labels[permission] || permission;
  }

  async saveEmployeePermissions(): Promise<void> {
    const employee = this.selectedEmployee();
    if (!employee) return;
    this.loading.set(true); this.error.set('');
    try {
      const permissions = this.availablePermissions().filter(permission => this.permissionSelection[permission]);
      await this.api.put(`/employees/${employee.id}/permissions`, { permissions });
      this.notice.set(`Đã cập nhật quyền cho ${employee.fullName}`);
      await this.refresh();
      const updated = this.employees().find(e => e.id === employee.id);
      if (updated) this.selectEmployee(updated);
    } catch { this.error.set('Không lưu được quyền nhân viên.'); }
    finally { this.loading.set(false); }
  }

  async setEmployeeActive(employee: Employee): Promise<void> {
    if (!window.confirm(`${employee.active ? 'Khóa' : 'Mở khóa'} tài khoản ${employee.fullName}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.patch(`/employees/${employee.id}/status`, { active: !employee.active });
      this.notice.set('Đã cập nhật trạng thái nhân viên');
      await this.refresh();
    } catch { this.error.set('Không cập nhật được trạng thái nhân viên.'); }
    finally { this.loading.set(false); }
  }

  async adjustStock(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      await this.api.post('/stock/adjustments', this.adjustment);
      this.adjustment = { productId: '', delta: 0, reason: '' };
      this.notice.set('Đã ghi nhận thay đổi kho và lịch sử');
      await this.refresh();
    } catch { this.error.set('Không điều chỉnh được kho. Kiểm tra tồn và lý do.'); }
    finally { this.loading.set(false); }
  }

  async openStocktake(id: string): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const session = await this.api.get<StocktakeSession>(`/stocktakes/${id}`);
      this.currentStocktake.set(session);
      this.actualByItem = Object.fromEntries(session.items.map(item => [item.id, item.actualQuantity ?? item.expectedQuantity]));
    } catch { this.error.set('Không tải được phiên kiểm kho.'); }
    finally { this.loading.set(false); }
  }

  async createStocktake(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const session = await this.api.post<StocktakeSession>('/stocktakes', {});
      this.currentStocktake.set(session);
      this.actualByItem = Object.fromEntries(session.items.map(item => [item.id, item.expectedQuantity]));
      this.notice.set('Đã tạo phiên kiểm kho. Hãy ghi số thực tế cho từng sản phẩm.');
      await this.refresh();
    } catch { this.error.set('Không tạo được phiên kiểm kho. Cần có sản phẩm trước.'); }
    finally { this.loading.set(false); }
  }

  async countStocktakeItem(itemId: string): Promise<void> {
    const session = this.currentStocktake();
    if (!session) return;
    this.loading.set(true); this.error.set('');
    try {
      this.currentStocktake.set(await this.api.put<StocktakeSession>(`/stocktakes/${session.id}/items/${itemId}`, { actualQuantity: this.actualByItem[itemId] }));
      this.notice.set('Đã lưu số lượng thực tế');
    } catch { this.error.set('Không lưu được số lượng. Kiểm tra phiên còn mở và số lượng không âm.'); }
    finally { this.loading.set(false); }
  }

  async completeStocktake(): Promise<void> {
    const session = this.currentStocktake();
    if (!session || !window.confirm('Xác nhận chênh lệch và cập nhật tồn kho?')) return;
    this.loading.set(true); this.error.set('');
    try {
      this.currentStocktake.set(await this.api.post<StocktakeSession>(`/stocktakes/${session.id}/complete`, {}));
      this.notice.set('Đã xác nhận kiểm kho và ghi lịch sử chênh lệch');
      await this.refresh();
    } catch { this.error.set('Không thể xác nhận. Hãy nhập đủ số lượng; nếu kho vừa thay đổi, tạo phiên mới.'); }
    finally { this.loading.set(false); }
  }

  addPurchaseLine(): void { this.purchaseLines.push({ productId: '', quantity: 1, unitCost: 0 }); }

  async createPurchase(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      await this.api.post('/purchases', { supplierId: this.selectedSupplierId || null, items: this.purchaseLines });
      this.purchaseLines = [{ productId: '', quantity: 1, unitCost: 0 }];
      this.selectedSupplierId = '';
      this.notice.set('Đã tạo phiếu nhập nháp');
      await this.refresh();
    } catch { this.error.set('Không tạo được phiếu nhập. Kiểm tra sản phẩm, số lượng và giá vốn.'); }
    finally { this.loading.set(false); }
  }

  async completePurchase(id: string): Promise<void> {
    if (!window.confirm('Hoàn tất phiếu nhập và tăng tồn kho?')) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.post(`/purchases/${id}/complete`, {});
      this.notice.set('Đã hoàn tất phiếu nhập và cập nhật tồn kho');
      await this.refresh();
    } catch { this.error.set('Không hoàn tất được phiếu nhập.'); }
    finally { this.loading.set(false); }
  }

  changeCart(productId: string, delta: number): void {
    if (this.pendingOrderId || sessionStorage.getItem('hbpos_checkout_key')) return;
    const product = this.products().find(p => p.id === productId);
    if (!product) return;
    const quantity = Math.max(0, Math.min(product.quantity, (this.cart()[productId] || 0) + delta));
    this.cart.update(old => ({ ...old, [productId]: quantity }));
    sessionStorage.setItem('hbpos_cart', JSON.stringify(this.cart()));
    this.paymentAmount = this.cartTotal();
  }

  async checkout(): Promise<void> {
    if (!this.cartItems().length && !this.pendingOrderId) return;
    this.loading.set(true); this.error.set('');
    try {
      if (!this.pendingOrderId) {
        const key = sessionStorage.getItem('hbpos_checkout_key') || crypto.randomUUID();
        sessionStorage.setItem('hbpos_checkout_key', key);
        const order = await this.api.post<SalesOrder>('/orders', {
          idempotencyKey: key,
          customerId: this.selectedCustomerId || null,
          discount: 0,
          items: this.cartItems().map(p => ({ productId: p.id, quantity: this.cart()[p.id] }))
        });
        this.pendingOrderId = order.id;
        sessionStorage.setItem('hbpos_pending_order', order.id);
      }
      if (this.paymentAmount > 0) {
        const paymentKey = sessionStorage.getItem('hbpos_payment_key') || crypto.randomUUID();
        sessionStorage.setItem('hbpos_payment_key', paymentKey);
        await this.api.post(`/orders/${this.pendingOrderId}/payments`, {
          idempotencyKey: paymentKey, method: this.paymentMethod, amount: this.paymentAmount
        });
      }
      this.pendingOrderId = null;
      this.cart.set({});
      this.selectedCustomerId = '';
      this.paymentAmount = 0;
      for (const key of ['hbpos_cart', 'hbpos_checkout_key', 'hbpos_pending_order', 'hbpos_payment_key']) sessionStorage.removeItem(key);
      this.page.set('orders');
      this.notice.set('Đã ghi nhận đơn bán hàng');
      await this.refresh();
    } catch (error) {
      if (!this.pendingOrderId && error instanceof HttpErrorResponse && [400, 409].includes(error.status))
        sessionStorage.removeItem('hbpos_checkout_key');
      this.error.set('Chưa hoàn tất đơn. Nếu lỗi do mạng, giỏ hàng và mã giao dịch được giữ để bạn thử lại mà không tạo đơn trùng.');
    }
    finally { this.loading.set(false); }
  }

  async openReturn(orderId: string): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      this.returnLines.set(await this.api.get<OrderLine[]>(`/orders/${orderId}/items`));
      this.returnOrderId = orderId;
      this.returnReason = '';
      this.returnRefundAmount = 0;
      this.returnQuantity = {};
      this.returnRestock = {};
    } catch { this.error.set('Không tải được chi tiết đơn hàng.'); }
    finally { this.loading.set(false); }
  }

  async openReceipt(orderId: string): Promise<void> {
    this.loading.set(true); this.error.set('');
    try { this.receipt.set(await this.api.get<OrderReceipt>(`/orders/${orderId}/receipt`)); }
    catch { this.error.set('Không tải được hóa đơn. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  formatVietnamTime(value: string): string {
    return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  }

  windowPrint(): void { window.print(); }

  async submitReturn(): Promise<void> {
    if (!this.returnOrderId) return;
    const items = this.returnLines().filter(line => (this.returnQuantity[line.id] || 0) > 0)
      .map(line => ({ orderItemId: line.id, quantity: this.returnQuantity[line.id], restock: !!this.returnRestock[line.id] }));
    if (!items.length || this.returnReason.trim().length < 5) {
      this.error.set('Chọn sản phẩm cần trả và nhập lý do ít nhất 5 ký tự.');
      return;
    }
    if (!window.confirm('Xác nhận trả hàng, hoàn tiền và cập nhật kho theo tình trạng đã chọn?')) return;
    this.loading.set(true); this.error.set('');
    try {
      const key = sessionStorage.getItem('hbpos_return_key') || crypto.randomUUID();
      sessionStorage.setItem('hbpos_return_key', key);
      await this.api.post(`/orders/${this.returnOrderId}/returns`, {
        idempotencyKey: key, reason: this.returnReason.trim(), refundAmount: this.returnRefundAmount,
        refundMethod: this.returnRefundMethod, items
      });
      sessionStorage.removeItem('hbpos_return_key');
      this.returnOrderId = '';
      this.returnLines.set([]);
      this.notice.set('Đã ghi nhận trả hàng và hoàn tiền');
      await this.refresh();
    } catch (error) {
      const detail = error instanceof HttpErrorResponse && typeof error.error?.error === 'string' ? error.error.error : '';
      this.error.set(detail || 'Không xử lý được trả hàng. Kiểm tra số lượng, tiền hoàn và tồn kho; mã giao dịch được giữ để thử lại.');
    }
    finally { this.loading.set(false); }
  }

  async loadReport(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try { this.report.set(await this.api.get<ReportSummary>(`/reports/summary?from=${this.reportFrom}&to=${this.reportTo}`)); }
    catch { this.error.set('Không tải được báo cáo. Khoảng ngày tối đa 367 ngày.'); }
    finally { this.loading.set(false); }
  }
}
