# Ứng dụng di động Miêu

Source Android và iOS nằm trong `HongBeautyPOSUI/android` và `HongBeautyPOSUI/ios`. Capacitor 8 đóng gói giao diện Angular, dùng cùng API và PostgreSQL của bản máy tính. Màn POS có nút quét mã vạch bằng camera trên bản native; plugin `@capacitor/barcode-scanner` yêu cầu Android minSdk 26 và quyền camera trên iOS. Chưa thử quét trên thiết bị thật. Giao diện mobile chuyên biệt, tối ưu toàn bộ thao tác một tay và thông báo đẩy vẫn chưa hoàn thiện.

## Cài và đồng bộ source

Máy phát triển cần Bun hoặc npm, môi trường Node.js 22+, Android Studio/Android SDK cho Android. iOS cần macOS và Xcode. Từ `HongBeautyPOSUI`, cài dependency bằng `bun install`, rồi chạy `bun run mobile:sync` sau mỗi lần sửa giao diện. Trong môi trường này đã tạo được cả hai native project và đồng bộ tài nguyên, nhưng chưa build APK/IPA vì thiếu Android SDK/Xcode.

## Android

Chạy `bun run mobile:android`; dự án mở trong Android Studio. Chọn emulator hoặc thiết bị rồi Run. Khi phát hành, tạo keystore riêng bên ngoài Git, ký bản release và xuất APK/AAB theo Android Studio.

## iOS

Trên máy Mac, chạy `bun run mobile:ios`. Trong Xcode, đặt Apple Team và Bundle ID, chọn simulator hoặc iPhone rồi Run. Phát hành IPA cần tài khoản Apple Developer và cấu hình ký ứng dụng.

## Kết nối máy chủ

Ở màn hình đăng nhập mobile, nhập **origin HTTPS** của backend, ví dụ `https://pos.congty-cua-ban.com`. Ứng dụng lưu địa chỉ này trên thiết bị; không nhập URL PostgreSQL, không nhập `/api/v1`, không đưa secret vào app. Backend phải đặt `CORS_ALLOWED_ORIGINS` gồm `capacitor://localhost` cho iOS và `http://localhost` cho Android, cùng origin web khác nếu có. Cần cài chứng chỉ TLS hợp lệ ở backend. Desktop vẫn gọi `/api/v1` cùng origin.

Sau khi đăng nhập, kiểm tra sản phẩm, quét mã vạch tại màn Bán hàng, kho, tạo đơn và tải lại dashboard trên thiết bị thứ hai để xác nhận cùng dữ liệu. Giao dịch mất kết nối cần tải lại trạng thái đơn trước khi thử lại; backend có khóa giao dịch cho các luồng đã triển khai.
