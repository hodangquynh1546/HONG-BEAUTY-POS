# Miêu — trạng thái khôi phục

## Đã khôi phục trong bản này

- Tên hiển thị Miêu trên web, Android và iOS.
- Mã nguồn Domain, Application, Infrastructure và 7 migration được khôi phục từ các bản dựng .NET còn lưu trong không gian làm việc. Đây là bản mã cũ, không phải bản sao của commit `914045c`.
- `render.yaml` triển khai API và giao diện cùng một dịch vụ Docker. Khi khởi động, dịch vụ áp dụng migration; nếu chưa có Chủ sở hữu thì tạo từ các biến môi trường được nhập riêng. `/health` chỉ báo khỏe khi kết nối database được và không còn migration chờ.
- `.env.example` và `docs/DEPLOYMENT.md` mô tả biến môi trường và các bước triển khai.

## Kiểm tra đã chạy

- .NET build và publish Release: đạt, còn cảnh báo từ mã khôi phục.
- Angular production build: đạt.
- PostgreSQL 16 riêng cho kiểm thử: áp dụng 7 migration thành công; khởi động với `RUN_MIGRATIONS_ON_START=true`, tạo Chủ sở hữu ban đầu, `/health` trả 200 và đăng nhập Chủ sở hữu trả 200.
- Tìm khách hàng theo tên, số điện thoại hoặc email có phân trang; màn hình khách hàng và quầy bán có ô tìm. PostgreSQL smoke test xác nhận lọc, phân trang, ký tự `%` là chữ thường, chặn tham số sai và yêu cầu đăng nhập.

## Chưa xác minh

- Không có mã nguồn hoặc đối tượng Git của commit `914045c` trong bản lưu hiện tại, nên không thể xác nhận mã mới trùng chức năng hoặc đúng số tệp của commit đó. Không thêm tệp chỉ để đạt một con số.
- Chưa chạy toàn bộ luồng nghiệp vụ trên PostgreSQL từ bản mã khôi phục; chưa thử Docker, Render, Neon, Android hay iOS trên thiết bị thật.
- Các tính năng được nhắc trong lịch sử sau bản dựng này, gồm tìm kiếm/phân trang đơn hàng, báo cáo công nợ, cấu hình cửa hàng và xử lý giao dịch khi mất mạng, cần được xây lại và kiểm thử riêng.

Chưa sử dụng bản này với dữ liệu kinh doanh thật trước khi kiểm thử đầy đủ các mục trên.
