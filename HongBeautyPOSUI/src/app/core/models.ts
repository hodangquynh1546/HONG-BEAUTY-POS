export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  role: 'Owner' | 'Employee';
  permissions: string[];
  avatarData: string | null;
}
export interface AttributeOption {
  id: string;
  name: string;
  value: string;
}
export interface Product {
  id: string;
  sku: string;
  barcode: string | null;
  name: string;
  unit: string;
  reorderLevel: number;
  salePrice: number;
  costPrice: number;
  quantity: number;
  active: boolean;
  categoryId: string | null;
  attributes?: AttributeOption[];
  imageData?: string | null;
}
export interface Category {
  id: string;
  name: string;
  parentId: string | null;
  active: boolean;
}
export interface Customer {
  id: string;
  fullName: string;
  phone: string | null;
  phone2: string | null;
  gender: string | null;
  address: string | null;
  contact: string | null;
  notes: string | null;
  groupId: string | null;
  avatarData: string | null;
  email: string | null;
  active: boolean;
}
export interface CustomerGroup {
  id: string;
  name: string;
  active: boolean;
}
export interface Supplier {
  id: string;
  code: string;
  name: string;
  phone: string | null;
  email: string | null;
  active: boolean;
}
export interface Employee {
  id: string;
  email: string;
  fullName: string;
  avatarData: string | null;
  active: boolean;
  permissions: string[];
}
export interface AuditEvent {
  id: string;
  actorId: string;
  actorName: string;
  action: string;
  subjectId: string;
  subjectName: string;
  createdAt: string;
  detailsJson: string | null;
}
export interface StockItem {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  reorderLevel: number;
}
export interface StockMovement {
  id: string;
  productId: string;
  productName: string;
  performerName: string;
  type: string;
  delta: number;
  before: number;
  after: number;
  reason: string;
  createdAt: string;
}
export interface StocktakeLine {
  id: string;
  productId: string;
  sku: string;
  name: string;
  expectedQuantity: number;
  actualQuantity: number | null;
}
export interface StocktakeSession {
  id: string;
  status: 'Draft' | 'Completed';
  createdAt: string;
  completedAt: string | null;
  items: StocktakeLine[];
}
export interface PurchaseReceipt {
  id: string;
  receiptNo: string;
  supplierId: string | null;
  status: 'Draft' | 'Completed' | 'Cancelled';
  total: number;
  createdAt: string;
}
export interface SalesOrder {
  id: string;
  orderNo: string;
  customerId: string | null;
  status: string;
  total: number;
  paidTotal: number;
  returnedTotal: number;
  refundedTotal: number;
  createdAt: string;
}
export interface OrderLine {
  id: string;
  productId: string;
  name: string;
  sku: string;
  quantity: number;
  unitPrice: number;
}
export interface OrderReceipt {
  id: string;
  orderNo: string;
  createdAt: string;
  status: string;
  customerName: string;
  sellerName: string;
  subtotal: number;
  discount: number;
  orderDiscount: number;
  total: number;
  paidTotal: number;
  returnedTotal: number;
  refundedTotal: number;
  remainingDebt: number;
  items: { skuSnapshot: string; nameSnapshot: string; quantity: number; unitPrice: number; discountPercent: number; discountAmount: number; lineTotal: number }[];
  payments: { method: string; amount: number; receivedAt: string }[];
}
export interface CashEntry {
  id: string;
  kind: string;
  method: string;
  amount: number;
  description: string;
  creatorName: string;
  createdAt: string;
}
export interface ReportSummary {
  from: string;
  to: string;
  grossSales: number;
  returnedSales: number;
  netRevenue: number;
  cashReceived: number;
  cashRefunded: number;
  netCashFlow: number;
  costOfGoods: number;
  estimatedGrossProfit: number;
  inventoryValue: number;
  orderCount: number;
  unitsSold: number;
  lowStockProducts: number;
  outOfStockProducts: number;
  otherIncome: number;
  operatingExpenses: number;
  supplierPayments: number;
  otherExpenses: number;
  estimatedNetProfit: number;
}
export interface PaymentMethodReport {
  method: 'Cash' | 'BankTransfer' | 'QrBanking';
  received: number;
  refunded: number;
  net: number;
}
export interface TopProductReport {
  productId: string;
  productName: string;
  soldQuantity: number;
  returnedQuantity: number;
  netQuantity: number;
}
export interface DailyReportPoint {
  date: string;
  grossSales: number;
  returnedSales: number;
  netRevenue: number;
  cashIn: number;
  cashOut: number;
  netCashFlow: number;
}
