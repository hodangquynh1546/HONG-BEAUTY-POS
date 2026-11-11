const VN_TZ = 'Asia/Ho_Chi_Minh';
const dayFormat = new Intl.DateTimeFormat('sv-SE', { timeZone: VN_TZ });
const dateTimeFormat = new Intl.DateTimeFormat('vi-VN', { timeZone: VN_TZ, dateStyle: 'short', timeStyle: 'short' });
const moneyFormat = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' });

/** `yyyy-MM-dd` for the given instant, as a calendar day in Vietnam. */
export const vietnamDay = (value: string | Date = new Date()): string => dayFormat.format(new Date(value));
export const formatVietnamTime = (value: string): string => dateTimeFormat.format(new Date(value));
export const formatMoney = (value: number): string => moneyFormat.format(value);
export const formatVnd = (value: number): string => `${value.toLocaleString('vi-VN')} đ`;
