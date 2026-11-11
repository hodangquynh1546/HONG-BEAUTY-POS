# Triển khai Miêu trên Render và Neon

## Chuẩn bị

1. Tạo một PostgreSQL database riêng trên Neon. Giữ URL kết nối ở phần bí mật của dịch vụ, không đưa vào Git hoặc tin nhắn.
2. Tạo Blueprint từ repository này trên Render. File `render.yaml` tạo một web service Docker gói Free.
3. Nhập các biến có `sync: false` trong cấu hình dịch vụ:
   - `DATABASE_URL`: URL kết nối PostgreSQL của Neon.
   - `JWT_SECRET`: chuỗi ngẫu nhiên riêng, ít nhất 32 ký tự.
   - `OWNER_EMAIL`, `OWNER_NAME`, `OWNER_PASSWORD`: tài khoản Chủ sở hữu ban đầu. Mật khẩu dài ít nhất 14 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.

`FORWARDED_HEADERS_ENABLED=true` (đã đặt sẵn trong `render.yaml`) để giới hạn đăng nhập theo IP thật của khách thay vì IP của proxy Render. Danh sách đầy đủ biến môi trường và ý nghĩa nằm trong `.env.example`.

`BOOTSTRAP_OWNER_ON_START=true` chỉ tạo Chủ sở hữu khi database chưa có ai giữ vai trò đó. Nếu chưa nhập đủ ba biến `OWNER_*`, dịch vụ vẫn khởi động nhưng chưa có tài khoản để đăng nhập; nhập các biến rồi triển khai lại. Sau khi đăng nhập thành công, có thể bỏ `OWNER_PASSWORD` khỏi môi trường và đặt `BOOTSTRAP_OWNER_ON_START=false`; lần khởi động sau sẽ không cần mật khẩu ban đầu. Không dùng cùng database cho test và production.

## Phiên đăng nhập

Access token sống ngắn (`JWT_ACCESS_MINUTES`, mặc định 15 phút) và được app tự gia hạn bằng refresh token xoay vòng (`REFRESH_TOKEN_DAYS`, mặc định 30 ngày không hoạt động). Mỗi lần gia hạn cấp refresh token mới; dùng lại token cũ sau hơn 15 giây sẽ thu hồi toàn bộ phiên đó. Đổi hoặc đặt lại mật khẩu, khóa tài khoản và đăng xuất đều chấm dứt phiên. Refresh token chỉ lưu dạng băm trong bảng `refresh_tokens`. Trình duyệt nhận refresh token qua cookie `HttpOnly; SameSite=Strict; Secure` (đường dẫn `/api/v1/auth`), script không đọc được, kèm header chống CSRF `X-Requested-With`. App Android/iOS khác origin nên gửi `X-Client: native` và tự giữ token. Cookie `Secure` chỉ được đặt khi yêu cầu đến qua HTTPS, vì vậy production bắt buộc có HTTPS và `FORWARDED_HEADERS_ENABLED=true` khi đứng sau proxy.

## Quên mật khẩu

- Nhân viên liên hệ Chủ sở hữu. Chủ sở hữu vào **Nhân viên → Phân quyền → Đặt lại mật khẩu**; mật khẩu cũ và phiên đăng nhập cũ hết hiệu lực ngay.
- Chủ sở hữu dùng quy trình khôi phục có quyền vận hành máy chủ: sao lưu database, đặt tạm `DATABASE_URL`, `JWT_SECRET`, `OWNER_EMAIL` đúng email đang có và `OWNER_PASSWORD` mới đủ mạnh trong môi trường riêng, rồi chạy ứng dụng với đối số `--recover-owner-password`. Lệnh chỉ đổi mật khẩu cho đúng Chủ sở hữu hiện hữu, ghi nhật ký và làm vô hiệu phiên cũ; không tạo Chủ sở hữu mới. Xóa `OWNER_PASSWORD` khỏi môi trường sau khi khôi phục. Không dùng đối số này làm lệnh khởi động web thường xuyên; không nhập mật khẩu vào trang đăng nhập công khai khi yêu cầu khôi phục.

## Xác minh sau triển khai

1. Đợi Render báo dịch vụ khỏe. `/health` chỉ trả 200 khi database kết nối được và không còn migration chờ áp dụng.
2. Mở trang đăng nhập, đăng nhập bằng tài khoản Chủ sở hữu vừa tạo, rồi thử đọc danh mục và kho.
3. Sao lưu database trước mỗi thay đổi schema; kiểm tra khả năng khôi phục trên một database riêng.

Đã thử migration, khởi tạo Chủ sở hữu, `/health` và đăng nhập trên PostgreSQL cục bộ. Chưa có kết quả kiểm thử Docker, Render hoặc Neon thực tế cho bản khôi phục này. Không dùng dữ liệu kinh doanh thật trước khi các bước trên được kiểm chứng.
