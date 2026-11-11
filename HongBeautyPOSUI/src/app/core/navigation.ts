export type Page = 'overview' | 'products' | 'categories' | 'customers' | 'suppliers' | 'stock' | 'stocktakes'
  | 'purchases' | 'pos' | 'orders' | 'cash' | 'reports' | 'employees' | 'audit';

export interface NavItem {
  page: Page;
  label: string;
  icon: string;
  /** Visible when any listed permission is held; `'owner'` restricts to the owner; omitted = everyone. */
  access?: readonly string[] | 'owner';
}

export const NAV_ITEMS: readonly NavItem[] = [
  { page: 'overview', label: 'Tổng quan', icon: 'dashboard' },
  { page: 'products', label: 'Sản phẩm', icon: 'inventory_2', access: ['products.read'] },
  { page: 'purchases', label: 'Nhập hàng', icon: 'input', access: ['purchases.read'] },
  { page: 'orders', label: 'Đơn hàng', icon: 'receipt_long', access: ['sales.read'] },
  { page: 'pos', label: 'Bán hàng', icon: 'point_of_sale', access: ['sales.write'] },
  { page: 'categories', label: 'Danh mục', icon: 'category', access: ['products.read'] },
  { page: 'customers', label: 'Khách hàng', icon: 'groups', access: ['customers.read'] },
  { page: 'suppliers', label: 'Nhà cung cấp', icon: 'local_shipping', access: ['suppliers.read'] },
  { page: 'stock', label: 'Tồn kho', icon: 'warehouse', access: ['inventory.read'] },
  { page: 'stocktakes', label: 'Kiểm kho', icon: 'fact_check', access: ['inventory.read'] },
  { page: 'cash', label: 'Thu chi', icon: 'payments', access: ['cash.read', 'cash.write'] },
  { page: 'reports', label: 'Báo cáo', icon: 'monitoring', access: ['reports.read'] },
  { page: 'employees', label: 'Nhân viên', icon: 'badge', access: 'owner' },
  { page: 'audit', label: 'Nhật ký', icon: 'history', access: 'owner' }
];

export const pageTitle = (page: Page): string => NAV_ITEMS.find(item => item.page === page)?.label ?? '';
