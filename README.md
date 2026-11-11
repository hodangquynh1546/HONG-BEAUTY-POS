# Miêu

Ứng dụng quản lý hàng hóa, kho và bán hàng cho một chủ cửa hàng. Repository đang triển khai; xem [PROJECT_STATUS.md](PROJECT_STATUS.md) để biết phần đã kiểm thử và phần chưa hoàn thành.

## Công nghệ

- Backend: .NET 10, ASP.NET Core, EF Core 10, PostgreSQL.
- Frontend: Angular 20, TypeScript 5.9; source Android/iOS đóng gói bằng Capacitor 8. Kendo UI theo đặc tả chưa tích hợp vì chưa có license.
- Một ứng dụng phục vụ Angular và API cùng origin.

Xem [MOBILE-SETUP.md](MOBILE-SETUP.md) để đồng bộ và build ứng dụng Android/iOS.

## Chạy local bằng Docker

1. Sao chép `.env.example` thành `.env` và thay tất cả mật khẩu/secret mẫu bằng giá trị mạnh, riêng biệt.
2. Chạy `docker compose up -d --build`. Compose chạy PostgreSQL, migration rồi khởi động ứng dụng tại `http://localhost:8080`.
3. Khởi tạo Owner một lần: đặt `OWNER_EMAIL`, `OWNER_NAME`, `OWNER_PASSWORD` trong môi trường shell, rồi chạy `docker compose run --rm -e OWNER_EMAIL -e OWNER_NAME -e OWNER_PASSWORD app --bootstrap-owner`. Không lưu mật khẩu Owner vào `.env` nếu không cần.
4. Kiểm tra `http://localhost:8080/health`, sau đó đăng nhập bằng tài khoản Owner vừa tạo.

Không lưu Owner password hoặc database URL thật vào Git. Bước bootstrap sẽ từ chối tạo Owner thứ hai. Nếu dùng Neon, gán `DATABASE_URL` bằng URI `postgresql://...` trong cấu hình môi trường của ứng dụng; backend tự chuyển sang định dạng Npgsql. Đừng dán URL này vào issue, chat hoặc log vì nó chứa mật khẩu.

## Chạy không dùng Docker

- Cần .NET SDK 10, Node.js 22+, PostgreSQL.
- Cung cấp `DATABASE_URL` và `JWT_SECRET` qua biến môi trường; chạy `dotnet run --project HongBeautyPOSAPI/src/HongBeautyPOS.Api -- --migrate` trước lần đầu.
- Chạy API bằng `dotnet run --project HongBeautyPOSAPI/src/HongBeautyPOS.Api --launch-profile http` (mặc định cổng 5024).
- Trong `HongBeautyPOSUI`, chạy `npm ci` rồi `npm start`; proxy `/api` chuyển đến API cổng 5024.
- Kiểm tra bản dựng: `dotnet build HongBeautyPOSAPI/HongBeautyPOS.slnx` và `npm run build` trong thư mục UI.
- Kiểm thử tích hợp trên **database PostgreSQL trống chỉ dành cho test**: sau khi build .NET, đặt `POS_TEST_DATABASE_URL` tới database đó rồi chạy `python3 tests/integration_smoke.py`. Bài test tự tạo Owner ngẫu nhiên và dữ liệu giao dịch; không chạy trên database production. Script chỉ chấp nhận host localhost.

## Kiểm thử e2e

Từ `HongBeautyPOSUI`: `npx playwright install chromium` (một lần) rồi `npm run e2e`. Script dựng PostgreSQL tạm bằng Docker, API phục vụ bản build Angular và chạy Playwright (desktop + mobile). Cần Docker đang chạy; không đụng database thật.

## Lưu ý triển khai

Production chưa sẵn sàng. Luồng cốt lõi đã được kiểm thử trên PostgreSQL cục bộ, nhưng Docker, build native Android/iOS và các phân hệ còn thiếu trong `PROJECT_STATUS.md` chưa được xác minh. Chưa nên dùng cho dữ liệu kinh doanh thật.

## AWS

Hướng dẫn từng bước triển khai lên một máy EC2 (Docker Compose + Caddy HTTPS + sao lưu S3): [docs/AWS-DEPLOY.md](docs/AWS-DEPLOY.md).

## Render và Neon

`render.yaml` định nghĩa một dịch vụ Docker chạy web và API cùng địa chỉ, tự áp dụng migration trước khi nhận truy cập. Xem [hướng dẫn triển khai](docs/DEPLOYMENT.md) để nhập biến môi trường và tạo tài khoản Chủ sở hữu lần đầu. Cấu hình đã được build cục bộ; chưa kiểm thử trên Render/Neon thực tế.
