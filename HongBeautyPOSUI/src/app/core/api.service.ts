import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Capacitor } from '@capacitor/core';

export interface CurrentUser { id: string; email: string; fullName: string; role: 'Owner' | 'Employee'; permissions: string[]; }
export interface AttributeOption { id: string; name: string; value: string; }
export interface Product { id: string; sku: string; barcode: string | null; name: string; unit: string; reorderLevel: number; salePrice: number; costPrice: number; quantity: number; active: boolean; categoryId: string | null; attributes?: AttributeOption[]; }
export interface Category { id: string; name: string; parentId: string | null; active: boolean; }
export interface Customer { id: string; fullName: string; phone: string | null; email: string | null; active: boolean; }
export interface Supplier { id: string; code: string; name: string; phone: string | null; email: string | null; active: boolean; }
export interface Employee { id: string; email: string; fullName: string; active: boolean; permissions: string[]; }
export interface AuditEvent { id: string; actorId: string; actorName: string; action: string; subjectId: string; subjectName: string; createdAt: string; }
export interface StockItem { productId: string; sku: string; name: string; quantity: number; reorderLevel: number; }
export interface StockMovement { id: string; productId: string; productName: string; performerName: string; type: string; delta: number; before: number; after: number; reason: string; createdAt: string; }
export interface StocktakeLine { id: string; productId: string; sku: string; name: string; expectedQuantity: number; actualQuantity: number | null; }
export interface StocktakeSession { id: string; status: 'Draft' | 'Completed'; createdAt: string; completedAt: string | null; items: StocktakeLine[]; }
export interface PurchaseReceipt { id: string; receiptNo: string; supplierId: string | null; status: 'Draft' | 'Completed' | 'Cancelled'; total: number; createdAt: string; }
export interface SalesOrder { id: string; orderNo: string; customerId: string | null; status: string; total: number; paidTotal: number; returnedTotal: number; refundedTotal: number; createdAt: string; }
export interface OrderLine { id: string; productId: string; name: string; sku: string; quantity: number; unitPrice: number; }
export interface OrderReceipt { id: string; orderNo: string; createdAt: string; status: string; customerName: string; sellerName: string; subtotal: number; discount: number; total: number; paidTotal: number; returnedTotal: number; refundedTotal: number; remainingDebt: number; items: { skuSnapshot: string; nameSnapshot: string; quantity: number; unitPrice: number; lineTotal: number }[]; payments: { method: string; amount: number; receivedAt: string }[]; }
export interface CashEntry { id: string; kind: string; method: string; amount: number; description: string; creatorName: string; createdAt: string; }
export interface ReportSummary { from: string; to: string; grossSales: number; returnedSales: number; netRevenue: number; cashReceived: number; cashRefunded: number; netCashFlow: number; costOfGoods: number; estimatedGrossProfit: number; inventoryValue: number; orderCount: number; unitsSold: number; lowStockProducts: number; outOfStockProducts: number; otherIncome: number; operatingExpenses: number; supplierPayments: number; otherExpenses: number; estimatedNetProfit: number; }
export interface PaymentMethodReport { method: 'Cash' | 'BankTransfer' | 'QrBanking'; received: number; refunded: number; net: number; }
export interface TopProductReport { productId: string; productName: string; soldQuantity: number; returnedQuantity: number; netQuantity: number; }
export interface DailyReportPoint { date: string; grossSales: number; returnedSales: number; netRevenue: number; cashIn: number; cashOut: number; netCashFlow: number; }

@Injectable({ providedIn: 'root' })
export class ApiService {
  readonly user = signal<CurrentUser | null>(null);
  private token = sessionStorage.getItem('hbpos_token') ?? '';
  readonly isNative = Capacitor.isNativePlatform();
  readonly serverUrl = signal(localStorage.getItem('hbpos_server_url') ?? '');
  constructor(private readonly http: HttpClient) {}
  setServerUrl(value: string): void {
    let parsed: URL;
    try { parsed = new URL(value.trim()); }
    catch { throw new Error('Địa chỉ máy chủ không hợp lệ.'); }
    if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.pathname !== '/' || parsed.search || parsed.hash) {
      throw new Error('Chỉ nhập địa chỉ HTTPS của máy chủ, không kèm đường dẫn.');
    }
    const url = parsed.origin;
    this.logout();
    localStorage.setItem('hbpos_server_url', url);
    this.serverUrl.set(url);
  }
  private get base(): string {
    if (this.isNative && !this.serverUrl()) throw new Error('Chưa cấu hình địa chỉ máy chủ.');
    return `${this.isNative ? this.serverUrl() : ''}/api/v1`;
  }
  async restore(): Promise<void> {
    if (!this.token) return;
    try { this.user.set(await this.get<CurrentUser>('/me')); }
    catch { this.logout(); }
  }
  async login(email: string, password: string): Promise<void> {
    const result = await this.post<{ accessToken: string }>('/auth/login', { email, password });
    this.token = result.accessToken;
    sessionStorage.setItem('hbpos_token', this.token);
    this.user.set(await this.get<CurrentUser>('/me'));
  }
  logout(): void { this.token = ''; sessionStorage.removeItem('hbpos_token'); this.user.set(null); }
  can(permission: string): boolean {
    const user = this.user();
    return user?.role === 'Owner' || !!user?.permissions.includes(permission);
  }
  private async request<T>(call: Promise<T>): Promise<T> {
    try { return await call; }
    catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401 && this.token) this.logout();
      throw error;
    }
  }
  get<T>(path: string): Promise<T> { return this.request(firstValueFrom(this.http.get<T>(this.base + path, { headers: this.headers() }))); }
  post<T>(path: string, body: unknown): Promise<T> { return this.request(firstValueFrom(this.http.post<T>(this.base + path, body, { headers: this.headers() }))); }
  put<T>(path: string, body: unknown): Promise<T> { return this.request(firstValueFrom(this.http.put<T>(this.base + path, body, { headers: this.headers() }))); }
  patch<T>(path: string, body: unknown): Promise<T> { return this.request(firstValueFrom(this.http.patch<T>(this.base + path, body, { headers: this.headers() }))); }
  private headers(): HttpHeaders { return this.token ? new HttpHeaders({ Authorization: `Bearer ${this.token}` }) : new HttpHeaders(); }
}
