/** Vietnamese display labels for backend enums and codes. Unknown codes fall back to a neutral label. */
const lookup = (table: Record<string, string>, fallback: string) => (key: string): string => table[key] ?? fallback;

const PERMISSIONS: Record<string, string> = {
  'products.read': 'Xem sản phẩm', 'products.write': 'Tạo sản phẩm, danh mục',
  'inventory.read': 'Xem tồn kho', 'inventory.adjust': 'Điều chỉnh và kiểm kho',
  'purchases.read': 'Xem phiếu nhập', 'purchases.write': 'Tạo và hoàn tất phiếu nhập',
  'sales.read': 'Xem đơn bán', 'sales.write': 'Bán hàng',
  'payments.write': 'Nhận thanh toán', 'returns.write': 'Trả hàng và hoàn tiền',
  'customers.read': 'Xem khách hàng', 'customers.write': 'Tạo khách hàng',
  'suppliers.read': 'Xem nhà cung cấp', 'suppliers.write': 'Tạo nhà cung cấp',
  'reports.read': 'Xem báo cáo', 'cash.read': 'Xem thu chi', 'cash.write': 'Ghi phiếu thu chi'
};

export const permissionLabel = (permission: string): string => PERMISSIONS[permission] ?? permission;

export const orderStatusLabel = lookup({
  Unpaid: 'Chưa thanh toán', PartiallyPaid: 'Thanh toán một phần', Paid: 'Đã thanh toán',
  PartiallyReturned: 'Đã trả một phần', Returned: 'Đã trả toàn bộ', Cancelled: 'Đã hủy'
}, 'Trạng thái chưa xác định');

export const paymentMethodLabel = lookup(
  { Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản', QrBanking: 'QR ngân hàng' }, 'Phương thức khác');

export const cashKindLabel = lookup({
  OtherIncome: 'Thu khác', OperatingExpense: 'Chi vận hành',
  SupplierPayment: 'Thanh toán nhà cung cấp', OtherExpense: 'Chi khác'
}, 'Giao dịch khác');

export const stockMovementTypeLabel = lookup({
  PURCHASE: 'Nhập hàng', SALE: 'Bán hàng', RETURN: 'Trả hàng', ADJUSTMENT: 'Điều chỉnh kho', STOCKTAKE: 'Kiểm kho'
}, 'Biến động kho');

export const auditActionLabel = lookup({
  EMPLOYEE_CREATED: 'Tạo nhân viên', EMPLOYEE_PERMISSIONS_REPLACED: 'Cập nhật quyền nhân viên',
  EMPLOYEE_PASSWORD_RESET: 'Đặt lại mật khẩu nhân viên', EMPLOYEE_ACTIVATED: 'Mở khóa nhân viên',
  EMPLOYEE_DEACTIVATED: 'Khóa nhân viên', PRODUCT_CREATED: 'Tạo sản phẩm', PRODUCT_UPDATED: 'Sửa sản phẩm',
  PRODUCT_ACTIVATED: 'Mở bán sản phẩm', PRODUCT_DEACTIVATED: 'Ngừng bán sản phẩm',
  CATEGORY_CREATED: 'Tạo danh mục', CATEGORY_UPDATED: 'Sửa danh mục', CATEGORY_ACTIVATED: 'Mở danh mục',
  CATEGORY_DEACTIVATED: 'Ngừng danh mục', CUSTOMER_CREATED: 'Tạo khách hàng', CUSTOMER_UPDATED: 'Sửa khách hàng',
  CUSTOMER_ACTIVATED: 'Mở khách hàng', CUSTOMER_DEACTIVATED: 'Ngừng khách hàng',
  CUSTOMER_GROUP_CREATED: 'Tạo nhóm khách hàng', SUPPLIER_CREATED: 'Tạo nhà cung cấp',
  SUPPLIER_UPDATED: 'Sửa nhà cung cấp', SUPPLIER_ACTIVATED: 'Mở nhà cung cấp', SUPPLIER_DEACTIVATED: 'Ngừng nhà cung cấp',
  SALE_RETURN_CREATED: 'Tạo phiếu trả hàng', CASH_ENTRY_CREATED: 'Ghi phiếu thu chi',
  PROFILE_UPDATED: 'Cập nhật tài khoản', PASSWORD_CHANGED: 'Đổi mật khẩu',
  OWNER_PASSWORD_RECOVERED: 'Khôi phục mật khẩu Chủ sở hữu', STOCKTAKE_DRAFT_DELETED: 'Xóa phiên kiểm kho nháp'
}, 'Thao tác hệ thống');

export const genderLabel = (gender: string | null | undefined): string =>
  ({ Female: 'Nữ', Male: 'Nam', Other: 'Khác' } as Record<string, string>)[gender ?? ''] ?? '—';

export const productStatusLabel = (p: { active: boolean; quantity: number; reorderLevel: number }): string =>
  !p.active ? 'Ngừng bán' : p.quantity === 0 ? 'Hết hàng' : p.quantity <= p.reorderLevel ? 'Sắp hết' : 'Đang bán';
