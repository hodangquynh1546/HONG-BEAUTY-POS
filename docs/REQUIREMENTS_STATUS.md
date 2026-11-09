# Đối chiếu Master Implementation Prompt

Kiểm tra trên source ở nhánh `main` ngày 2026-11-09. `training-doc.md` không có trong repository hoặc `/workspace`, nên chưa thể xác nhận quy ước kỹ thuật được tài liệu đó chỉ định. Stack đang có là Angular 20, TypeScript 5.9, .NET 10, EF Core 10, PostgreSQL và Capacitor 8; chưa thay framework hay database.

| Nhóm yêu cầu | Trạng thái từ source hiện tại | Phần cần hoàn thiện / xác minh |
| --- | --- | --- |
| Một backend, một database, Desktop và Mobile | Web và API cùng origin; Android/iOS là native project Capacitor dùng cùng API | Chưa build hoặc thử trên thiết bị thật; thiếu một số màn hình mobile riêng |
| Owner, Employee, permission | Có hai role và kiểm tra quyền ở API; Owner bootstrap; quản lý nhân viên, đặt lại mật khẩu và vô hiệu phiên cũ khi đổi mật khẩu/khóa tài khoản; người dùng đổi tên và mật khẩu; Chủ sở hữu có lệnh khôi phục riêng | Cần kiểm thử toàn bộ ma trận quyền và hoàn thiện chỉnh sửa thông tin nhân viên; chưa có luồng tự khôi phục mật khẩu qua email |
| Dashboard | Có báo cáo tổng hợp lấy từ PostgreSQL, bộ lọc nhanh/tùy chọn theo ngày Việt Nam, đơn mới, hàng bán chạy và phân bố thanh toán theo quyền | Thiếu biểu đồ doanh thu và thu chi; chưa có kiểm thử giao diện tự động cho mọi bộ lọc |
| Sản phẩm và danh mục | Có CRUD/status, SKU, mã vạch, đơn vị, ngưỡng cảnh báo, tồn và danh mục cha/con; tìm theo tên, SKU hoặc mã vạch | Thiếu import/export Excel, nhiều ảnh, biến thể, thương hiệu và bộ lọc đầy đủ |
| Kho, nhập và kiểm kho | Có phiếu nhập, điều chỉnh, stock movement, kiểm kho và giao dịch database; giao diện có tổng quan tồn cùng lịch sử nhập/xuất theo tên sản phẩm, người thao tác, tồn trước/sau, lọc và phân trang | Thiếu vòng đời Draft/Cancelled của phiếu nhập, báo hàng tồn lâu, xuất kho theo lý do chuẩn hóa; cần thử tải đồng thời |
| POS và thanh toán | Có tạo đơn idempotency, trừ kho, thanh toán từng phần; xem/in hóa đơn từ dữ liệu thật và lưu PDF qua hộp thoại in của trình duyệt | Thiếu kết hợp phương thức ngay trên màn hình POS, QR, xuất PDF tự động, chỉnh/hủy đơn theo quy tắc, thử mất mạng |
| Trả hàng và hoàn tiền | Có API trả một phần, điều kiện nhập lại kho và số tiền hoàn | Cần kiểm thử luồng tài chính đầy đủ, chứng từ hoàn và UI chi tiết |
| Khách hàng và nhà cung cấp | Có CRUD/status và tìm khách theo tên/điện thoại/email | Thiếu lịch sử giao dịch/công nợ, phân nhóm, xuất Excel và tìm nhà cung cấp |
| Thu chi và báo cáo | Có màn ghi/xem thu chi từ database, chống trùng khi gửi lại; summary tách doanh thu, tiền thu, giá vốn và các khoản chi; báo cáo thu/hoàn/thực nhận theo phương thức thanh toán và 10 sản phẩm bán chạy | Thiếu thu công nợ khách, phân loại/đối soát nâng cao, báo cáo ngày/nhân viên, Excel/PDF |
| Cài đặt, audit, backup | Có audit events và biến môi trường thiết yếu | Thiếu giao diện cấu hình cửa hàng, backup/restore và chính sách lưu dữ liệu lâu dài |
| Mobile | Có source Android/iOS Capacitor, dùng cùng Angular/API; POS có nút quét camera bằng plugin native | Chưa build APK/IPA hoặc thử quét trên thiết bị; thiếu thông báo và tối ưu một số luồng một tay |
| Docker, Render, Neon | Có Dockerfile, `docker-compose.yml`, `render.yaml` và hướng dẫn | Docker engine vắng trong môi trường này nên chưa chạy thử Compose; chưa xác minh Render/Neon thật |
| Kiểm thử | Có integration smoke trên PostgreSQL cục bộ và build .NET/Angular | Thiếu unit suite, API integration suite đầy đủ và E2E 19 bước; chưa đạt điều kiện nghiệm thu cuối |

Chưa thể đánh dấu dự án hoàn thành theo Master Implementation Prompt. Mọi tính năng thiếu phải được triển khai và kiểm chứng trước khi dùng với dữ liệu kinh doanh thật.
