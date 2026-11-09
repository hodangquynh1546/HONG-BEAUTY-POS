import { Component, HostListener, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService, type CurrentUser, type Employee, type AuditEvent, type Product, type AttributeOption, type StockItem, type StockMovement, type StocktakeSession, type PurchaseReceipt, type SalesOrder, type OrderLine, type OrderReceipt, type ReportSummary, type PaymentMethodReport, type TopProductReport, type DailyReportPoint, type Category, type Customer, type CustomerGroup, type Supplier, type CashEntry } from './core/api.service';

type Page = 'overview' | 'products' | 'categories' | 'customers' | 'suppliers' | 'stock' | 'stocktakes' | 'purchases' | 'pos' | 'orders' | 'cash' | 'reports' | 'employees' | 'audit';
type Overlay = 'account' | 'product' | 'customer' | 'permissions' | 'stock' | 'cash' | 'return' | 'auditDetail' | null;

@Component({
  selector: 'app-root',
  imports: [FormsModule, CurrencyPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  readonly page = signal<Page>('overview');
  readonly sidebarCollapsed = signal(localStorage.getItem('mieu_sidebar_collapsed') === 'true');
  readonly overlay = signal<Overlay>(null);
  readonly confirmation = signal<{ title: string; message: string; action: string } | null>(null);
  private resolveConfirmation: ((accepted: boolean) => void) | null = null;
  readonly forgotPasswordInfo = signal(false);
  profileName = '';
  profileAvatarData = '';
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  readonly products = signal<Product[]>([]);
  readonly posProducts = signal<Product[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly attributeOptions = signal<AttributeOption[]>([]);
  readonly attributeNames = computed(() => [...new Set(this.attributeOptions().map(x => x.name))]);
  selectedAttributeOptions: Record<string, boolean> = {};
  quickCategoryOpen = false;
  quickCategoryName = '';
  quickAttributeOpen = false;
  quickAttributeName = '';
  quickAttributeValues = '';
  readonly customers = signal<Customer[]>([]);
  readonly customerGroups = signal<CustomerGroup[]>([]);
  readonly selectedAuditEvent = signal<AuditEvent | null>(null);
  quickCustomerGroupOpen = false;
  quickCustomerGroupName = '';
  readonly customerTotal = signal(0);
  readonly posCustomers = signal<Customer[]>([]);
  readonly suppliers = signal<Supplier[]>([]);
  readonly employees = signal<Employee[]>([]);
  readonly auditEvents = signal<AuditEvent[]>([]);
  readonly cashEntries = signal<CashEntry[]>([]);
  readonly availablePermissions = signal<string[]>([]);
  readonly selectedEmployeeId = signal('');
  readonly selectedEmployee = computed(() => this.employees().find(e => e.id === this.selectedEmployeeId()) ?? null);
  readonly stock = signal<StockItem[]>([]);
  readonly stockQuantity = computed(() => this.stock().reduce((total, item) => total + item.quantity, 0));
  readonly lowStockCount = computed(() => this.stock().filter(item => item.quantity > 0 && item.quantity <= item.reorderLevel).length);
  readonly outStockCount = computed(() => this.stock().filter(item => item.quantity <= 0).length);
  readonly stockMovements = signal<StockMovement[]>([]);
  readonly stockMovementTotal = signal(0);
  stockMovementProductId = '';
  readonly stocktakes = signal<StocktakeSession[]>([]);
  readonly currentStocktake = signal<StocktakeSession | null>(null);
  readonly purchases = signal<PurchaseReceipt[]>([]);
  readonly orders = signal<SalesOrder[]>([]);
  readonly recentOrders = signal<SalesOrder[]>([]);
  readonly receipt = signal<OrderReceipt | null>(null);
  readonly orderTotal = signal(0);
  readonly report = signal<ReportSummary | null>(null);
  readonly paymentMethodReport = signal<PaymentMethodReport[]>([]);
  readonly topProductsReport = signal<TopProductReport[]>([]);
  readonly dailyTrend = signal<DailyReportPoint[]>([]);
  readonly monthlyChart = signal(false);
  readonly chartPoints = computed(() => {
    const points = this.dailyTrend();
    if (!this.monthlyChart()) return points.slice(-31);
    const months = new Map<string, DailyReportPoint>();
    for (const point of points) {
      const key = point.date.slice(0, 7);
      const current = months.get(key) ?? { date: key + '-01', grossSales: 0, returnedSales: 0, netRevenue: 0, cashIn: 0, cashOut: 0, netCashFlow: 0 };
      months.set(key, { ...current, grossSales: current.grossSales + point.grossSales, returnedSales: current.returnedSales + point.returnedSales, netRevenue: current.netRevenue + point.netRevenue, cashIn: current.cashIn + point.cashIn, cashOut: current.cashOut + point.cashOut, netCashFlow: current.netCashFlow + point.netCashFlow });
    }
    return [...months.values()].slice(-12);
  });
  readonly revenueScale = computed(() => Math.max(1, ...this.chartPoints().map(point => Math.abs(point.netRevenue))));
  readonly cashScale = computed(() => Math.max(1, ...this.chartPoints().flatMap(point => [point.cashIn, point.cashOut])));
  readonly returnLines = signal<OrderLine[]>([]);
  readonly cart = signal<Record<string, number>>(JSON.parse(sessionStorage.getItem('hbpos_cart') || '{}'));
  readonly lineDiscountPercents = signal<Record<string, number>>(JSON.parse(sessionStorage.getItem('hbpos_line_discounts') || '{}'));
  readonly discountEditorProductId = signal('');
  readonly cartItems = computed(() => this.products().filter(p => (this.cart()[p.id] || 0) > 0));
  readonly cartTotal = computed(() => this.cartItems().reduce((total, p) => total + p.salePrice * this.cart()[p.id], 0));
  readonly lineDiscountTotal = computed(() => this.cartItems().reduce((total, p) => total + Math.round(p.salePrice * this.cart()[p.id] * (this.lineDiscountPercents()[p.id] || 0) / 100), 0));
  readonly discountPercent = signal(Number(sessionStorage.getItem('hbpos_order_discount') || 0));
  readonly discountAmount = computed(() => Math.round((this.cartTotal() - this.lineDiscountTotal()) * this.discountPercent() / 100));
  readonly payableTotal = computed(() => this.cartTotal() - this.lineDiscountTotal() - this.discountAmount());
  readonly loading = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  email = '';
  password = '';
  showLoginPassword = false;
  serverUrlInput = '';
  newProduct = { sku: '', barcode: '', name: '', unit: 'cái', reorderLevel: 0, salePrice: 0, costPrice: 0, categoryId: '', imageData: '' };
  editingProductId = '';
  newCategory = { name: '', parentId: '' };
  editingCategoryId = '';
  newCustomer = { fullName: '', phone: '', phone2: '', email: '', gender: '', address: '', contact: '', notes: '', groupId: '', avatarData: '' };
  editingCustomerId = '';
  customerSearch = '';
  posCustomerSearch = '';
  quickPosCustomerOpen = false;
  quickPosCustomer = { fullName: '', phone: '', email: '' };
  posProductSearch = '';
  productListSearch = '';
  productListCategory = '';
  productListStock = '';
  categoryListSearch = '';
  categoryListStatus = '';
  categoryListParent = '';
  purchaseListSearch = '';
  purchaseListStatus = '';
  purchaseListSupplier = '';
  purchaseListFrom = '';
  purchaseListTo = '';
  orderSearch = '';
  orderStatus = '';
  orderFrom = '';
  orderTo = '';
  orderSellerId = '';
  newSupplier = { code: '', name: '', phone: '', email: '' };
  editingSupplierId = '';
  newEmployee = { email: '', fullName: '', password: '', avatarData: '' };
  newCashEntry = { kind: 'OperatingExpense', method: 'Cash', amount: 0, description: '' };
  readonly cashPending = signal(!!sessionStorage.getItem('hbpos_cash_pending'));
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
  reportPeriod: 'today' | 'yesterday' | 'last7' | 'thisMonth' | 'lastMonth' | 'custom' = 'today';

  constructor(readonly api: ApiService) {}

  filteredProducts(): Product[] {
    const term = this.productListSearch.trim().toLocaleLowerCase('vi');
    return this.products().filter(product =>
      (!term || [product.name, product.sku, product.barcode ?? ''].some(value => value.toLocaleLowerCase('vi').includes(term))) &&
      (!this.productListCategory || product.categoryId === this.productListCategory) &&
      (!this.productListStock || (this.productListStock === 'active' ? product.active :
        this.productListStock === 'inactive' ? !product.active :
        this.productListStock === 'out' ? product.active && product.quantity <= 0 :
        this.productListStock === 'low' ? product.active && product.quantity > 0 && product.quantity <= product.reorderLevel : true)));
  }

  filteredCategories(): Category[] {
    const term = this.categoryListSearch.trim().toLocaleLowerCase('vi');
    return this.categories().filter(category =>
      (!term || category.name.toLocaleLowerCase('vi').includes(term)) &&
      (!this.categoryListParent || (this.categoryListParent === 'root' ? !category.parentId : category.parentId === this.categoryListParent)) &&
      (!this.categoryListStatus || (this.categoryListStatus === 'active' ? category.active : !category.active)));
  }

  filteredPurchases(): PurchaseReceipt[] {
    const term = this.purchaseListSearch.trim().toLocaleLowerCase('vi');
    return this.purchases().filter(receipt =>
      (!term || receipt.receiptNo.toLocaleLowerCase('vi').includes(term)) &&
      (!this.purchaseListStatus || receipt.status === this.purchaseListStatus) &&
      (!this.purchaseListSupplier || receipt.supplierId === this.purchaseListSupplier) &&
      (!this.purchaseListFrom || this.vietnamDate(receipt.createdAt) >= this.purchaseListFrom) &&
      (!this.purchaseListTo || this.vietnamDate(receipt.createdAt) <= this.purchaseListTo));
  }

  private vietnamDate(value: string): string {
    return new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value));
  }

  categoryName(id: string | null): string {
    return id ? this.categories().find(category => category.id === id)?.name || '—' : '—';
  }

  supplierName(id: string | null): string {
    return id ? this.suppliers().find(supplier => supplier.id === id)?.name || '—' : 'Không chọn';
  }

  productAttributeLabel(product: Product): string {
    const grouped = new Map<string, string[]>();
    for (const option of product.attributes ?? []) grouped.set(option.name, [...(grouped.get(option.name) ?? []), option.value]);
    return [...grouped].map(([name, values]) => `${name}: ${values.join(', ')}`).join(' · ');
  }

  canCompleteStocktake(session: StocktakeSession): boolean {
    return session.items.length > 0 && session.items.every(item => item.actualQuantity !== null);
  }

  async ngOnInit(): Promise<void> {
    this.serverUrlInput = this.api.serverUrl();
    await this.api.restore();
    this.profileName = this.api.user()?.fullName ?? '';
    this.profileAvatarData = this.api.user()?.avatarData ?? '';
    const pendingCash = sessionStorage.getItem('hbpos_cash_pending');
    if (pendingCash) {
      try {
        const saved = JSON.parse(pendingCash);
        if (saved && typeof saved.idempotencyKey === 'string' && typeof saved.amount === 'number' &&
            typeof saved.description === 'string' && typeof saved.kind === 'string' && typeof saved.method === 'string')
          this.newCashEntry = { kind: saved.kind, method: saved.method, amount: saved.amount, description: saved.description };
        else { sessionStorage.removeItem('hbpos_cash_pending'); this.cashPending.set(false); }
      } catch { sessionStorage.removeItem('hbpos_cash_pending'); this.cashPending.set(false); }
    }
    if (this.api.user()) await this.refresh();
    if (!this.pendingOrderId) this.paymentAmount = this.payableTotal();
  }

  toggleSidebar(): void {
    this.sidebarCollapsed.update(value => !value);
    localStorage.setItem('mieu_sidebar_collapsed', String(this.sidebarCollapsed()));
  }

  openOverlay(kind: Exclude<Overlay, null>): void {
    this.error.set('');
    this.overlay.set(kind);
  }

  closeOverlay(): void {
    if (this.loading()) return;
    if (this.overlay() === 'product') this.cancelProductEdit();
    if (this.overlay() === 'customer') this.cancelCustomerEdit();
    this.overlay.set(null);
    this.selectedEmployeeId.set('');
    this.returnOrderId = '';
    this.currentPassword = this.newPassword = this.confirmPassword = '';
  }

  private askConfirmation(message: string, action = 'Xác nhận'): Promise<boolean> {
    return new Promise(resolve => {
      this.resolveConfirmation = resolve;
      this.confirmation.set({ title: 'Xác nhận thao tác', message, action });
    });
  }

  answerConfirmation(accepted: boolean): void {
    this.confirmation.set(null);
    this.resolveConfirmation?.(accepted);
    this.resolveConfirmation = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.confirmation()) this.answerConfirmation(false);
    else if (this.overlay()) this.closeOverlay();
  }

  createProduct(): void { this.cancelProductEdit(); this.openOverlay('product'); }
  createCustomer(): void { this.cancelCustomerEdit(); this.openOverlay('customer'); }

  async chooseProductImage(event: Event, target: 'product' | 'customer' | 'profile' | 'employee' = 'product'): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/') || file.size > 10_000_000) { this.error.set('Chọn ảnh dưới 10 MB.'); input.value = ''; return; }
    const url = URL.createObjectURL(file);
    try {
      const photo = new Image();
      photo.src = url;
      await photo.decode();
      const scale = Math.min(1, 640 / Math.max(photo.naturalWidth, photo.naturalHeight));
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(photo.naturalWidth * scale));
      canvas.height = Math.max(1, Math.round(photo.naturalHeight * scale));
      const context = canvas.getContext('2d');
      if (!context) throw new Error('Không xử lý được ảnh.');
      context.fillStyle = '#fff';
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.drawImage(photo, 0, 0, canvas.width, canvas.height);
      let image = canvas.toDataURL('image/jpeg', .78);
      if (image.length > 210_000) image = canvas.toDataURL('image/jpeg', .55);
      if (image.length > 210_000) throw new Error('Ảnh sau khi nén vẫn quá lớn. Hãy chọn ảnh đơn giản hơn.');
      if (target === 'product') this.newProduct.imageData = image;
      else if (target === 'customer') this.newCustomer.avatarData = image;
      else if (target === 'employee') this.newEmployee.avatarData = image;
      else this.profileAvatarData = image;
      this.error.set('');
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'Không đọc được ảnh.'); }
    finally { URL.revokeObjectURL(url); input.value = ''; }
  }

  async addQuickCategory(): Promise<void> {
    if (!this.quickCategoryName.trim()) return;
    this.loading.set(true); this.error.set('');
    try {
      const created = await this.api.post<Category>('/categories', { name: this.quickCategoryName.trim(), parentId: null });
      this.categories.set([...this.categories(), created].sort((a, b) => a.name.localeCompare(b.name, 'vi')));
      this.newProduct.categoryId = created.id;
      this.quickCategoryName = '';
      this.quickCategoryOpen = false;
    } catch { this.error.set('Không thêm được danh mục. Vui lòng kiểm tra tên.'); }
    finally { this.loading.set(false); }
  }

  async addQuickAttribute(): Promise<void> {
    const values = this.quickAttributeValues.split(',').map(x => x.trim()).filter(Boolean);
    if (!this.quickAttributeName.trim() || !values.length) { this.error.set('Nhập tên thuộc tính và ít nhất một giá trị.'); return; }
    this.loading.set(true); this.error.set('');
    try {
      const options = await this.api.post<AttributeOption[]>('/attributes', { name: this.quickAttributeName.trim(), values });
      const byId = new Map([...this.attributeOptions(), ...options].map(x => [x.id, x]));
      this.attributeOptions.set([...byId.values()].sort((a, b) => a.name.localeCompare(b.name, 'vi') || a.value.localeCompare(b.value, 'vi')));
      for (const option of options) this.selectedAttributeOptions[option.id] = true;
      this.quickAttributeOpen = false;
      this.quickAttributeName = this.quickAttributeValues = '';
    } catch { this.error.set('Không thêm được thuộc tính. Tên tối đa 80 ký tự, mỗi giá trị tối đa 100 ký tự.'); }
    finally { this.loading.set(false); }
  }

  async saveProfile(): Promise<void> {
    this.loading.set(true); this.error.set(''); this.notice.set('');
    try {
      await this.api.put('/me/profile', { fullName: this.profileName, avatarData: this.profileAvatarData });
      this.api.user.set(await this.api.get<CurrentUser>('/me'));
      this.notice.set('Đã cập nhật tài khoản.');
    } catch { this.error.set('Không cập nhật được tên. Vui lòng kiểm tra lại.'); }
    finally { this.loading.set(false); }
  }

  async changeOwnPassword(): Promise<void> {
    if (this.newPassword !== this.confirmPassword) { this.error.set('Mật khẩu xác nhận không khớp.'); return; }
    this.loading.set(true); this.error.set(''); this.notice.set('');
    try {
      await this.api.put('/me/password', { currentPassword: this.currentPassword, newPassword: this.newPassword });
      this.currentPassword = ''; this.newPassword = ''; this.confirmPassword = '';
      this.api.logout();
      this.notice.set('Đã đổi mật khẩu. Vui lòng đăng nhập lại.');
    } catch { this.error.set('Không đổi được mật khẩu. Kiểm tra mật khẩu hiện tại và dùng mật khẩu mới 14–128 ký tự gồm chữ hoa, chữ thường, số, ký tự đặc biệt.'); }
    finally { this.loading.set(false); }
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

  async addPosCustomer(): Promise<void> {
    if (!this.api.can('customers.write')) return;
    this.loading.set(true); this.error.set('');
    try {
      const customer = await this.api.post<Customer>('/customers', this.quickPosCustomer);
      this.posCustomers.set([customer, ...this.posCustomers()]);
      this.customers.set([customer, ...this.customers()]);
      this.selectedCustomerId = customer.id;
      this.quickPosCustomer = { fullName: '', phone: '', email: '' };
      this.quickPosCustomerOpen = false;
      this.notice.set('Đã thêm và chọn khách hàng cho đơn bán.');
    } catch { this.error.set('Không thêm được khách hàng. Kiểm tra tên, số điện thoại và email.'); }
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
      await this.api.login(this.email, this.password); this.password = ''; this.notice.set(''); this.profileName = this.api.user()?.fullName ?? ''; await this.refresh();
    }
    catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 429) this.error.set('Bạn đã thử đăng nhập nhiều lần. Vui lòng chờ khoảng 1 phút rồi thử lại.');
      else if (error instanceof HttpErrorResponse && error.status === 401) this.error.set('Email hoặc mật khẩu không đúng.');
      else this.error.set(error instanceof Error && (error.message.includes('máy chủ') || error.message.includes('HTTPS')) ? error.message : 'Không đăng nhập được. Kiểm tra máy chủ và kết nối.');
    }
    finally { this.loading.set(false); }
  }

  async refresh(): Promise<void> {
    this.loading.set(true); this.error.set('');
    const failed: string[] = [];
    const attempt = async (label: string, load: () => Promise<void>): Promise<void> => {
      try { await load(); }
      catch { failed.push(label); }
    };
    try {
      if (this.api.can('products.read')) {
        await attempt('sản phẩm', async () => {
          this.products.set(await this.api.get<Product[]>('/products'));
          this.posProducts.set(this.products());
        });
        await attempt('danh mục', async () => this.categories.set(await this.api.get<Category[]>('/categories')));
        await attempt('thuộc tính', async () => this.attributeOptions.set(await this.api.get<AttributeOption[]>('/attributes')));
      }
      if (this.api.can('customers.read')) {
        await attempt('khách hàng', async () => this.loadCustomers(true));
        await attempt('nhóm khách hàng', async () => this.customerGroups.set(await this.api.get<CustomerGroup[]>('/customer-groups')));
        await attempt('khách tại quầy', async () => this.loadPosCustomers());
      }
      if (this.api.can('suppliers.read')) await attempt('nhà cung cấp', async () => this.suppliers.set(await this.api.get<Supplier[]>('/suppliers')));
      if (this.api.can('inventory.read')) {
        await attempt('tồn kho', async () => this.stock.set(await this.api.get<StockItem[]>('/stock')));
        await attempt('lịch sử kho', async () => this.loadStockMovements(true));
        await attempt('kiểm kho', async () => this.stocktakes.set(await this.api.get<StocktakeSession[]>('/stocktakes')));
      }
      if (this.api.can('purchases.read')) await attempt('phiếu nhập', async () => this.purchases.set(await this.api.get<PurchaseReceipt[]>('/purchases')));
      if (this.api.can('cash.read')) await attempt('thu chi', async () => this.cashEntries.set(await this.api.get<CashEntry[]>('/cash-entries')));
      if (this.api.can('sales.read')) {
        await attempt('đơn hàng', async () => this.loadOrders(true));
        await attempt('đơn mới', async () => {
          const recent = await this.api.get<{ items: SalesOrder[] }>('/orders/search?skip=0&take=5');
          this.recentOrders.set(recent.items);
        });
        const pending = this.orders().find(o => o.id === this.pendingOrderId);
        if (pending) this.paymentAmount = Math.max(0, pending.total - pending.paidTotal);
      }
      if (this.api.can('reports.read')) await attempt('báo cáo', async () => this.fetchReport());
      if (this.api.user()?.role === 'Owner') {
        await attempt('nhân viên', async () => this.employees.set(await this.api.get<Employee[]>('/employees')));
        await attempt('danh sách quyền', async () => this.availablePermissions.set(await this.api.get<string[]>('/permissions')));
        await attempt('nhật ký', async () => this.auditEvents.set(await this.api.get<AuditEvent[]>('/audit-events')));
      }
      if (failed.length) this.error.set(`Không tải được: ${failed.join(', ')}. Các phần khác vẫn sử dụng được; bấm Làm mới để thử lại.`);
    } catch { this.error.set('Không tải được dữ liệu. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  editProduct(product: Product): void {
    this.editingProductId = product.id;
    this.newProduct = { sku: product.sku, barcode: product.barcode ?? '', name: product.name, unit: product.unit, reorderLevel: product.reorderLevel, salePrice: product.salePrice, costPrice: product.costPrice, categoryId: product.categoryId ?? '', imageData: product.imageData ?? '' };
    this.selectedAttributeOptions = Object.fromEntries((product.attributes ?? []).map(x => [x.id, true]));
    this.openOverlay('product');
  }

  cancelProductEdit(): void {
    this.editingProductId = '';
    this.newProduct = { sku: '', barcode: '', name: '', unit: 'cái', reorderLevel: 0, salePrice: 0, costPrice: 0, categoryId: '', imageData: '' };
    this.selectedAttributeOptions = {};
    this.quickCategoryOpen = this.quickAttributeOpen = false;
    this.quickCategoryName = this.quickAttributeName = this.quickAttributeValues = '';
  }

  async saveProduct(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const payload = { ...this.newProduct, categoryId: this.newProduct.categoryId || null,
        attributeOptionIds: Object.entries(this.selectedAttributeOptions).filter(([, selected]) => selected).map(([id]) => id) };
      if (this.editingProductId) await this.api.put(`/products/${this.editingProductId}`, payload);
      else await this.api.post('/products', payload);
      this.notice.set(this.editingProductId ? 'Đã cập nhật sản phẩm' : 'Đã tạo sản phẩm');
      this.cancelProductEdit();
      this.overlay.set(null);
      await this.refresh();
    } catch { this.error.set('Không lưu được sản phẩm. Kiểm tra SKU, danh mục và giá trị nhập.'); }
    finally { this.loading.set(false); }
  }

  async setProductActive(product: Product): Promise<void> {
    if (!await this.askConfirmation(`${product.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} sản phẩm ${product.name}?`)) return;
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
    this.error.set('');
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
    if (!await this.askConfirmation(`${category.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} danh mục ${category.name}?`)) return;
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
    this.newCustomer = { fullName: customer.fullName, phone: customer.phone ?? '', phone2: customer.phone2 ?? '', email: customer.email ?? '', gender: customer.gender ?? '', address: customer.address ?? '', contact: customer.contact ?? '', notes: customer.notes ?? '', groupId: customer.groupId ?? '', avatarData: customer.avatarData ?? '' };
    this.openOverlay('customer');
  }

  cancelCustomerEdit(): void {
    this.editingCustomerId = '';
    this.newCustomer = { fullName: '', phone: '', phone2: '', email: '', gender: '', address: '', contact: '', notes: '', groupId: '', avatarData: '' };
    this.quickCustomerGroupOpen = false;
  }

  customerGroupName(id: string | null): string { return this.customerGroups().find(x => x.id === id)?.name ?? 'Chưa phân nhóm'; }

  async addQuickCustomerGroup(): Promise<void> {
    if (!this.quickCustomerGroupName.trim()) return;
    this.loading.set(true); this.error.set('');
    try {
      const group = await this.api.post<CustomerGroup>('/customer-groups', { name: this.quickCustomerGroupName.trim() });
      this.customerGroups.set([...this.customerGroups(), group].sort((a, b) => a.name.localeCompare(b.name, 'vi')));
      this.newCustomer.groupId = group.id;
      this.quickCustomerGroupName = ''; this.quickCustomerGroupOpen = false;
    } catch { this.error.set('Không tạo được nhóm khách hàng. Tên có thể đã tồn tại.'); }
    finally { this.loading.set(false); }
  }

  async saveCustomer(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      if (this.editingCustomerId) await this.api.put(`/customers/${this.editingCustomerId}`, { ...this.newCustomer, groupId: this.newCustomer.groupId || null, gender: this.newCustomer.gender || null });
      else await this.api.post('/customers', { ...this.newCustomer, groupId: this.newCustomer.groupId || null, gender: this.newCustomer.gender || null });
      this.notice.set(this.editingCustomerId ? 'Đã cập nhật khách hàng' : 'Đã tạo khách hàng');
      this.cancelCustomerEdit();
      this.overlay.set(null);
      await this.refresh();
    } catch { this.error.set('Không lưu được khách hàng. Kiểm tra thông tin nhập.'); }
    finally { this.loading.set(false); }
  }

  async setCustomerActive(customer: Customer): Promise<void> {
    if (!await this.askConfirmation(`${customer.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} khách hàng ${customer.fullName}?`)) return;
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
    this.error.set('');
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
    if (!await this.askConfirmation(`${supplier.active ? 'Ngừng sử dụng' : 'Kích hoạt lại'} nhà cung cấp ${supplier.name}?`)) return;
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
      this.newEmployee = { email: '', fullName: '', password: '', avatarData: '' };
      this.notice.set('Đã tạo nhân viên');
      await this.refresh();
    } catch { this.error.set('Không tạo được nhân viên. Kiểm tra thông tin và email trùng.'); }
    finally { this.loading.set(false); }
  }

  selectEmployee(employee: Employee): void {
    this.selectedEmployeeId.set(employee.id);
    this.resetEmployeePasswordInput = '';
    this.permissionSelection = Object.fromEntries(this.availablePermissions().map(permission => [permission, employee.permissions.includes(permission)]));
    this.openOverlay('permissions');
  }

  async resetEmployeePassword(): Promise<void> {
    const employee = this.selectedEmployee();
    if (!employee || !await this.askConfirmation(`Đặt lại mật khẩu cho ${employee.fullName}?`)) return;
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
      'reports.read': 'Xem báo cáo', 'cash.read': 'Xem thu chi', 'cash.write': 'Ghi phiếu thu chi'
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
    if (!await this.askConfirmation(`${employee.active ? 'Khóa' : 'Mở khóa'} tài khoản ${employee.fullName}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.patch(`/employees/${employee.id}/status`, { active: !employee.active });
      this.notice.set('Đã cập nhật trạng thái nhân viên');
      await this.refresh();
    } catch { this.error.set('Không cập nhật được trạng thái nhân viên.'); }
    finally { this.loading.set(false); }
  }

  async adjustStock(): Promise<void> {
    const item = this.stock().find(x => x.productId === this.adjustment.productId);
    const delta = Number(this.adjustment.delta);
    if (!item) { this.error.set('Hãy chọn sản phẩm cần điều chỉnh.'); return; }
    if (!Number.isFinite(delta) || delta === 0 || Math.abs(delta * 1000 - Math.round(delta * 1000)) > 1e-7) { this.error.set('Số lượng thay đổi phải khác 0 và có tối đa 3 chữ số thập phân.'); return; }
    if (item.quantity + delta < 0) { this.error.set(`Sản phẩm chỉ còn ${item.quantity}; không thể giảm ${Math.abs(delta)}.`); return; }
    if (this.adjustment.reason.trim().length < 5) { this.error.set('Lý do phải có ít nhất 5 ký tự.'); return; }
    if (!await this.askConfirmation(`Điều chỉnh ${item.name}: ${item.quantity} → ${item.quantity + delta}. Lý do: ${this.adjustment.reason.trim()}?`)) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.post('/stock/adjustments', this.adjustment);
      this.adjustment = { productId: '', delta: 0, reason: '' };
      this.overlay.set(null);
      this.notice.set('Đã ghi nhận thay đổi kho và lịch sử');
      await this.refresh();
    } catch (error) { this.error.set(this.apiErrorMessage(error, 'Không điều chỉnh được kho. Vui lòng tải lại tồn kho rồi thử lại.')); }
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

  async deleteStocktake(session: StocktakeSession): Promise<void> {
    if (session.status !== 'Draft' || !await this.askConfirmation('Xóa phiên kiểm kho chưa xác nhận này?')) return;
    this.loading.set(true); this.error.set('');
    try {
      await this.api.delete(`/stocktakes/${session.id}`);
      if (this.currentStocktake()?.id === session.id) this.currentStocktake.set(null);
      this.notice.set('Đã xóa phiên kiểm kho chưa xác nhận');
      await this.refresh();
    } catch (error) { this.error.set(this.apiErrorMessage(error, 'Không xóa được phiên kiểm kho.')); }
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
    if (!session || !await this.askConfirmation('Xác nhận chênh lệch và cập nhật tồn kho?')) return;
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
    if (!await this.askConfirmation('Hoàn tất phiếu nhập và tăng tồn kho?')) return;
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
    if (quantity === 0) {
      this.lineDiscountPercents.update(old => { const next = { ...old }; delete next[productId]; return next; });
      sessionStorage.setItem('hbpos_line_discounts', JSON.stringify(this.lineDiscountPercents()));
      if (this.discountEditorProductId() === productId) this.discountEditorProductId.set('');
    }
    this.paymentAmount = this.payableTotal();
  }

  lineDiscountValue(product: Product): number {
    return Math.round(product.salePrice * (this.cart()[product.id] || 0) * (this.lineDiscountPercents()[product.id] || 0) / 100);
  }

  updateLineDiscount(productId: string, value: number): void {
    if (this.pendingOrderId || sessionStorage.getItem('hbpos_checkout_key')) return;
    const percent = Number.isFinite(Number(value)) ? Math.max(0, Math.min(100, Math.round(Number(value) * 100) / 100)) : 0;
    this.lineDiscountPercents.update(old => ({ ...old, [productId]: percent }));
    sessionStorage.setItem('hbpos_line_discounts', JSON.stringify(this.lineDiscountPercents()));
    this.paymentAmount = this.payableTotal();
  }

  updateDiscountPercent(value: number): void {
    this.discountPercent.set(Number.isFinite(Number(value)) ? Math.max(0, Math.min(100, Number(value))) : 0);
    sessionStorage.setItem('hbpos_order_discount', String(this.discountPercent()));
    if (!this.pendingOrderId) this.paymentAmount = this.payableTotal();
  }

  async checkout(): Promise<void> {
    if (!this.cartItems().length && !this.pendingOrderId) return;
    if (!Number.isFinite(this.discountPercent()) || this.discountPercent() < 0 || this.discountPercent() > 100) { this.error.set('Giảm giá phải từ 0% đến 100%.'); return; }
    if (!this.pendingOrderId && !await this.askConfirmation(`Tạo đơn ${this.payableTotal().toLocaleString('vi-VN')} đ sau giảm giá sản phẩm ${this.lineDiscountTotal().toLocaleString('vi-VN')} đ và giảm toàn đơn ${this.discountAmount().toLocaleString('vi-VN')} đ; ghi nhận ${this.paymentAmount.toLocaleString('vi-VN')} đ thanh toán?`, 'Tạo đơn')) return;
    this.loading.set(true); this.error.set('');
    try {
      if (!this.pendingOrderId) {
        const key = sessionStorage.getItem('hbpos_checkout_key') || crypto.randomUUID();
        sessionStorage.setItem('hbpos_checkout_key', key);
        const order = await this.api.post<SalesOrder>('/orders', {
          idempotencyKey: key,
          customerId: this.selectedCustomerId || null,
          discount: this.discountAmount(),
          items: this.cartItems().map(p => ({ productId: p.id, quantity: this.cart()[p.id], discountPercent: this.lineDiscountPercents()[p.id] || 0 }))
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
      this.lineDiscountPercents.set({});
      this.discountEditorProductId.set('');
      this.discountPercent.set(0);
      this.selectedCustomerId = '';
      this.paymentAmount = 0;
      for (const key of ['hbpos_cart', 'hbpos_line_discounts', 'hbpos_order_discount', 'hbpos_checkout_key', 'hbpos_pending_order', 'hbpos_payment_key']) sessionStorage.removeItem(key);
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
      this.openOverlay('return');
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

  orderStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Unpaid: 'Chưa thanh toán', PartiallyPaid: 'Thanh toán một phần', Paid: 'Đã thanh toán',
      PartiallyReturned: 'Đã trả một phần', Returned: 'Đã trả toàn bộ', Cancelled: 'Đã hủy'
    };
    return labels[status] ?? 'Trạng thái chưa xác định';
  }

  paymentMethodLabel(method: string): string {
    return ({ Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản', QrBanking: 'QR ngân hàng' } as Record<string, string>)[method] ?? 'Phương thức khác';
  }

  cashKindLabel(kind: string): string {
    return ({ OtherIncome: 'Thu khác', OperatingExpense: 'Chi vận hành', SupplierPayment: 'Thanh toán nhà cung cấp', OtherExpense: 'Chi khác' } as Record<string, string>)[kind] ?? 'Giao dịch khác';
  }

  stockMovementTypeLabel(type: string): string {
    return ({ PURCHASE: 'Nhập hàng', SALE: 'Bán hàng', RETURN: 'Trả hàng', ADJUSTMENT: 'Điều chỉnh kho', STOCKTAKE: 'Kiểm kho' } as Record<string, string>)[type] ?? 'Biến động kho';
  }

  private async loadStockMovements(reset: boolean): Promise<void> {
    const params = new URLSearchParams({ skip: String(reset ? 0 : this.stockMovements().length), take: '50' });
    if (this.stockMovementProductId) params.set('productId', this.stockMovementProductId);
    const result = await this.api.get<{ items: StockMovement[]; total: number }>(`/stock/movements?${params}`);
    this.stockMovements.set(reset ? result.items : [...this.stockMovements(), ...result.items]);
    this.stockMovementTotal.set(result.total);
  }

  async searchStockMovements(reset = true): Promise<void> {
    this.loading.set(true); this.error.set('');
    try { await this.loadStockMovements(reset); }
    catch { this.error.set('Không tải được lịch sử kho. Vui lòng thử lại.'); }
    finally { this.loading.set(false); }
  }

  async saveCashEntry(): Promise<void> {
    if (!this.cashPending() && (!Number.isFinite(Number(this.newCashEntry.amount)) || Number(this.newCashEntry.amount) <= 0 || !this.newCashEntry.description.trim())) {
      this.error.set('Nhập số tiền lớn hơn 0 và nội dung thu chi.'); return;
    }
    if (!this.cashPending() && !await this.askConfirmation(`Ghi ${this.newCashEntry.amount.toLocaleString('vi-VN')} đ vào sổ thu chi?`, 'Ghi phiếu')) return;
    this.loading.set(true); this.error.set('');
    let savedSuccessfully = false;
    try {
      const saved = sessionStorage.getItem('hbpos_cash_pending');
      const payload = saved ? JSON.parse(saved) : { ...this.newCashEntry, idempotencyKey: crypto.randomUUID() };
      if (!saved) sessionStorage.setItem('hbpos_cash_pending', JSON.stringify(payload));
      this.cashPending.set(true);
      await this.api.post('/cash-entries', payload);
      sessionStorage.removeItem('hbpos_cash_pending');
      this.cashPending.set(false);
      this.newCashEntry = { kind: 'OperatingExpense', method: 'Cash', amount: 0, description: '' };
      this.overlay.set(null);
      this.notice.set('Đã ghi nhận phiếu thu chi');
      savedSuccessfully = true;
    } catch (error) {
      if (error instanceof HttpErrorResponse && [400, 409].includes(error.status)) {
        sessionStorage.removeItem('hbpos_cash_pending');
        this.cashPending.set(false);
      }
      this.error.set(this.apiErrorMessage(error, 'Chưa xác nhận được phiếu. Nếu mất mạng, bấm thử lại để dùng đúng mã giao dịch cũ, tránh ghi trùng.'));
    } finally { this.loading.set(false); }
    if (savedSuccessfully) {
      try {
        if (this.api.can('cash.read')) this.cashEntries.set(await this.api.get<CashEntry[]>('/cash-entries'));
        if (this.api.can('reports.read')) this.report.set(await this.api.get<ReportSummary>(`/reports/summary?from=${this.reportFrom}&to=${this.reportTo}`));
      } catch { this.notice.set('Phiếu đã lưu; chưa tải lại được danh sách hoặc báo cáo. Bấm Làm mới để xem.'); }
    }
  }

  async cancelPendingCash(): Promise<void> {
    if (!await this.askConfirmation('Chỉ bỏ phiếu chờ sau khi đã kiểm tra lịch sử thu chi. Nếu giao dịch đã được lưu, ghi phiếu mới có thể tạo trùng. Bạn muốn bỏ phiếu chờ?', 'Bỏ phiếu chờ')) return;
    sessionStorage.removeItem('hbpos_cash_pending');
    this.cashPending.set(false);
    this.newCashEntry = { kind: 'OperatingExpense', method: 'Cash', amount: 0, description: '' };
    this.error.set('');
  }

  private apiErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 403) return 'Tài khoản chưa được cấp quyền thực hiện thao tác này.';
      if (error.status === 401) return 'Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.';
      if (typeof error.error?.error === 'string') return error.error.error;
    }
    return fallback;
  }

  openAuditDetail(event: AuditEvent): void {
    this.selectedAuditEvent.set(event);
    this.openOverlay('auditDetail');
  }

  auditDetailRows(event: AuditEvent): { label: string; value: string }[] {
    if (!event.detailsJson) return [{ label: 'Chi tiết', value: 'Nhật ký cũ không lưu nội dung thay đổi.' }];
    try {
      const data = JSON.parse(event.detailsJson) as Record<string, unknown>;
      const names: Record<string, string> = {
        fullName: 'Họ tên', email: 'Email', permissions: 'Quyền được cấp',
        oldPermissions: 'Quyền trước đây', newPermissions: 'Quyền sau cập nhật',
        hasAvatar: 'Ảnh đại diện', hasImage: 'Ảnh sản phẩm', createdAt: 'Ngày tạo phiên', itemCount: 'Số sản phẩm',
        name: 'Tên', code: 'Mã', sku: 'SKU', barcode: 'Mã vạch', unit: 'Đơn vị',
        salePrice: 'Giá bán', costPrice: 'Giá vốn', reorderLevel: 'Ngưỡng cảnh báo',
        categoryId: 'Danh mục', parentId: 'Danh mục cha', groupId: 'Nhóm khách hàng',
        phone: 'Điện thoại', phone2: 'Điện thoại 2', gender: 'Giới tính', address: 'Địa chỉ',
        contact: 'Liên hệ', notes: 'Ghi chú', attributes: 'Thuộc tính'
      };
      const format = (key: string, value: unknown): string => {
        if (Array.isArray(value)) return value.length ? value.map(v => key.toLowerCase().includes('permission') ? this.permissionLabel(String(v)) : String(v)).join(', ') : 'Không có';
        if (typeof value === 'boolean') return value ? 'Có' : 'Không';
        if (value == null || value === '') return '—';
        if (key === 'createdAt') return this.formatVietnamTime(String(value));
        if (key === 'categoryId' || key === 'parentId') return this.categoryName(String(value));
        if (key === 'groupId') return this.customerGroupName(String(value));
        if (key === 'gender') return value === 'Female' ? 'Nữ' : value === 'Male' ? 'Nam' : value === 'Other' ? 'Khác' : '—';
        if (['salePrice', 'costPrice'].includes(key)) return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(Number(value));
        return String(value);
      };
      const previous = data['before'] as Record<string, unknown> | undefined;
      const current = data['after'] as Record<string, unknown> | undefined;
      if (previous && current) return Object.entries(current).filter(([key, value]) => JSON.stringify(previous[key]) !== JSON.stringify(value)).map(([key, value]) => {
        const normalized = key[0].toLowerCase() + key.slice(1);
        return { label: names[normalized] ?? normalized, value: `${format(normalized, previous[key])} → ${format(normalized, value)}` };
      });
      return Object.entries(data).map(([key, value]) => {
        const normalized = key[0].toLowerCase() + key.slice(1);
        return { label: names[normalized] ?? normalized, value: format(normalized, value) };
      });
    } catch { return [{ label: 'Chi tiết', value: 'Không đọc được nội dung thay đổi.' }]; }
  }

  auditActionLabel(action: string): string {
    const labels: Record<string, string> = {
      EMPLOYEE_CREATED: 'Tạo nhân viên', EMPLOYEE_PERMISSIONS_REPLACED: 'Cập nhật quyền nhân viên',
      EMPLOYEE_PASSWORD_RESET: 'Đặt lại mật khẩu nhân viên', EMPLOYEE_ACTIVATED: 'Mở khóa nhân viên',
      EMPLOYEE_DEACTIVATED: 'Khóa nhân viên', PRODUCT_UPDATED: 'Sửa sản phẩm',
      PRODUCT_ACTIVATED: 'Mở bán sản phẩm', PRODUCT_DEACTIVATED: 'Ngừng bán sản phẩm',
      CATEGORY_UPDATED: 'Sửa danh mục', CATEGORY_ACTIVATED: 'Mở danh mục', CATEGORY_DEACTIVATED: 'Ngừng danh mục',
      CUSTOMER_UPDATED: 'Sửa khách hàng', CUSTOMER_ACTIVATED: 'Mở khách hàng', CUSTOMER_DEACTIVATED: 'Ngừng khách hàng', CUSTOMER_GROUP_CREATED: 'Tạo nhóm khách hàng',
      SUPPLIER_UPDATED: 'Sửa nhà cung cấp', SUPPLIER_ACTIVATED: 'Mở nhà cung cấp', SUPPLIER_DEACTIVATED: 'Ngừng nhà cung cấp',
      SALE_RETURN_CREATED: 'Tạo phiếu trả hàng', CASH_ENTRY_CREATED: 'Ghi phiếu thu chi',
      PROFILE_UPDATED: 'Cập nhật tài khoản', PASSWORD_CHANGED: 'Đổi mật khẩu',
      OWNER_PASSWORD_RECOVERED: 'Khôi phục mật khẩu Chủ sở hữu', STOCKTAKE_DRAFT_DELETED: 'Xóa phiên kiểm kho nháp',
      PRODUCT_CREATED: 'Tạo sản phẩm', CATEGORY_CREATED: 'Tạo danh mục', CUSTOMER_CREATED: 'Tạo khách hàng', SUPPLIER_CREATED: 'Tạo nhà cung cấp'
    };
    return labels[action] ?? 'Thao tác hệ thống';
  }

  orderNo(orderId: string): string {
    return this.orders().find(order => order.id === orderId)?.orderNo ?? 'đang tải';
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
    if (!await this.askConfirmation('Xác nhận trả hàng, hoàn tiền và cập nhật kho theo tình trạng đã chọn?')) return;
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
      this.overlay.set(null);
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
    try { await this.fetchReport(); }
    catch (error) { this.error.set(error instanceof Error ? error.message : 'Không tải được báo cáo. Khoảng ngày tối đa 367 ngày.'); }
    finally { this.loading.set(false); }
  }

  async selectReportPeriod(period: 'today' | 'yesterday' | 'last7' | 'thisMonth' | 'lastMonth'): Promise<void> {
    this.reportPeriod = period;
    const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
    const date = new Date(`${today}T00:00:00Z`);
    const asDay = (value: Date) => value.toISOString().slice(0, 10);
    if (period === 'today') { this.reportFrom = today; this.reportTo = today; }
    if (period === 'yesterday') { date.setUTCDate(date.getUTCDate() - 1); this.reportFrom = asDay(date); this.reportTo = this.reportFrom; }
    if (period === 'last7') { this.reportTo = today; date.setUTCDate(date.getUTCDate() - 6); this.reportFrom = asDay(date); }
    if (period === 'thisMonth') { this.reportFrom = today.slice(0, 8) + '01'; this.reportTo = today; }
    if (period === 'lastMonth') { date.setUTCDate(0); this.reportTo = asDay(date); date.setUTCDate(1); this.reportFrom = asDay(date); }
    await this.loadReport();
  }

  revenueBarWidth(value: number): number {
    return value === 0 ? 0 : Math.max(2, Math.abs(value) / this.revenueScale() * 100);
  }

  methodBarWidth(value: number): number {
    const max = Math.max(1, ...this.paymentMethodReport().map(x => Math.abs(x.net)));
    return value === 0 ? 0 : Math.max(2, Math.abs(value) / max * 100);
  }

  productBarWidth(value: number): number {
    const max = Math.max(1, ...this.topProductsReport().map(x => x.netQuantity));
    return value <= 0 ? 0 : Math.max(2, value / max * 100);
  }

  private async fetchReport(): Promise<void> {
    const range = `from=${encodeURIComponent(this.reportFrom)}&to=${encodeURIComponent(this.reportTo)}`;
    const monthly = this.reportPeriod === 'thisMonth' || this.reportPeriod === 'lastMonth' || (this.reportPeriod === 'custom' && (Date.parse(this.reportTo) - Date.parse(this.reportFrom)) > 62 * 86400000);
    this.monthlyChart.set(monthly);
    const chartTo = this.reportTo;
    const selectedMonth = new Date(`${this.reportTo.slice(0, 7)}-01T00:00:00Z`);
    selectedMonth.setUTCMonth(selectedMonth.getUTCMonth() - 11);
    const chartFrom = monthly && this.reportPeriod !== 'custom' ? selectedMonth.toISOString().slice(0, 10) : this.reportFrom;
    const chartRange = `from=${encodeURIComponent(chartFrom)}&to=${encodeURIComponent(chartTo)}`;
    const [summary, methods, topProducts, daily] = await Promise.allSettled([
      this.api.get<ReportSummary>(`/reports/summary?${range}`),
      this.api.get<PaymentMethodReport[]>(`/reports/payment-methods?${range}`),
      this.api.get<TopProductReport[]>(`/reports/top-products?${range}`),
      this.api.get<DailyReportPoint[]>(`/reports/daily?${chartRange}`)
    ]);
    this.report.set(summary.status === 'fulfilled' ? summary.value : null);
    this.paymentMethodReport.set(methods.status === 'fulfilled' ? methods.value : []);
    this.topProductsReport.set(topProducts.status === 'fulfilled' ? topProducts.value : []);
    this.dailyTrend.set(daily.status === 'fulfilled' ? daily.value : []);
    const failed = [summary, methods, topProducts, daily]
      .map((result, index) => result.status === 'rejected' ? ['tổng hợp', 'thanh toán', 'sản phẩm bán chạy', 'biểu đồ'][index] : '')
      .filter(Boolean);
    if (failed.length) throw new Error(`Không tải được báo cáo ${failed.join(', ')}. Kiểm tra khoảng ngày rồi thử lại.`);
  }
}
