# Miêu — tiến độ thực tế

## Quyết định kiến trúc

- Theo nội dung `training-doc.md` chủ đã gửi: Angular 20, TypeScript 5.9, .NET 10, EF Core 10, PostgreSQL, Api/Application/Domain/Infrastructure.
- Một ứng dụng .NET phục vụ cả API `/api/v1` và Angular SPA cùng origin.
- Bản nháp Bun/React trước khi có tài liệu đã chuyển khỏi repository POS; không dùng làm sản phẩm.
- Chỉ có role Owner và Employee; quyền nhân viên lưu riêng. Owner được tạo bằng bước bootstrap, không có tài khoản mặc định.
- `DATABASE_URL` nhận cả Npgsql keyword-value string và URI Neon `postgresql://`.

## Đã có mã

- Backend: đăng nhập JWT, bootstrap Owner, quản lý nhân viên và quyền, danh mục (tạo/sửa/ngừng dùng, chặn vòng lặp cấp cha), khách hàng và nhà cung cấp (tạo/sửa/ngừng dùng/kích hoạt), sản phẩm (tạo/sửa/ngừng bán/kích hoạt). Có điều chỉnh và lịch sử tồn kho, nhập hàng, kiểm kho có phiên và đối chiếu chênh lệch, bán hàng, thanh toán một phần hoặc toàn bộ. API trả hàng/hoàn tiền với idempotency và cập nhật kho theo tình trạng hàng. Báo cáo tổng hợp doanh thu, dòng tiền, giá vốn và tồn kho lấy từ PostgreSQL. Có audit event cho nhân viên, danh mục, khách hàng, nhà cung cấp, sản phẩm và trả hàng.
- Angular: đăng nhập, tổng quan cơ bản, danh mục, khách hàng, nhà cung cấp và sản phẩm đều có tạo/sửa/ngừng dùng/kích hoạt; nhân viên (cấp/thu hồi quyền, khóa/mở), nhật ký thao tác, kho, nhập hàng, kiểm kho, POS, đơn hàng, trả hàng và báo cáo dùng API thật. Đã thêm dự án native Android/iOS qua Capacitor 8, dùng cùng Angular và API; mobile nhập origin HTTPS lúc đăng nhập.
- EF migrations: InitialCore, StockMovementReason, PurchasesAndSuppliers, SalesAndPayments, Stocktakes, SalesReturns, AuditEvents. Có chế độ `--migrate` riêng.
- Dockerfile một artifact, Docker Compose với PostgreSQL, bước migration và ứng dụng, `.env.example`.

## Kiểm thử đã chạy

- `.NET 10 test`: **20/20 đạt**. Có test chuyển đổi URL Neon, validation kiểm kho và trả hàng.
- `Angular production build`: **đạt** sau khi thêm cấu hình máy chủ mobile; `Capacitor sync` Android/iOS **đạt**. Chưa build native.
- PostgreSQL 16.15 chạy cục bộ: **migration 7/7 áp dụng thành công**; `tests/integration_smoke.py` **đạt** trên database trống cho bootstrap Owner, đăng nhập, giới hạn quyền Employee, quyền mới có hiệu lực, khóa nhân viên, audit event, danh mục (sửa, chặn vòng lặp, chặn ngừng cha đang có con, tắt/bật), khách hàng/nhà cung cấp (sửa, tắt/bật), sản phẩm (tạo/sửa/ngừng bán/kích hoạt), nhập kho, bán hàng, thanh toán, trả một phần có hoàn tiền/nhập lại kho, kiểm kho và số liệu báo cáo. Test bảo đảm số bán 200, trả 100, tiền ròng 100, giá vốn 50.
- Đăng nhập có giới hạn 5 lần/phút theo IP kết nối; kiểm tra trực tiếp 5 lần sai trả 401 và lần thứ 6 trả 429.
- Chưa chạy Docker Compose vì môi trường không có Docker; chưa kiểm thử concurrency thực.

## Chưa hoàn thành

- Thu chi, báo cáo mở rộng, cài đặt và các trường nghiệp vụ chi tiết của sản phẩm/khách hàng/nhà cung cấp.
- Bảo mật production: refresh/revocation, validation đầy đủ, kiểm thử thêm các trường hợp phân quyền và proxy cho giới hạn đăng nhập.
- Kendo UI 23.3 cần license; hiện Angular dùng component tự tạo. Mobile đã có dự án Android/iOS nhưng chưa build APK/IPA vì môi trường thiếu Android SDK/Xcode; chưa có UX một tay, barcode camera hay thông báo đẩy.
- Playwright, mở rộng integration test PostgreSQL cho phân quyền/concurrency, Docker verification, Render/Neon deployment và backup guide.
- Chưa push GitHub. Remote đã trỏ đúng repository nhưng chủ cần xem source/quyền trước khi push theo yêu cầu.

## Bước tiếp theo

1. Thêm thu chi và mở rộng báo cáo chi tiết, xử lý quyền và concurrency trên PostgreSQL thật.
2. Hoàn thiện cập nhật, khóa/ngừng sử dụng các danh mục dữ liệu và giao diện tương ứng.
3. Hoàn thiện Angular/Kendo, mobile packaging, bảo mật, triển khai và tài liệu.
