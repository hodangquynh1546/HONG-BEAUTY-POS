# Miêu — trạng thái khôi phục

Đối chiếu từng nhóm yêu cầu của Master Implementation Prompt tại [docs/REQUIREMENTS_STATUS.md](docs/REQUIREMENTS_STATUS.md). Dự án **chưa đạt điều kiện hoàn thành** của prompt.

## Đã khôi phục trong bản này

- Tên hiển thị Miêu trên web, Android và iOS.
- Mã nguồn Domain, Application, Infrastructure và 7 migration được khôi phục từ các bản dựng .NET còn lưu trong không gian làm việc. Đây là bản mã cũ, không phải bản sao của commit `914045c`.
- `render.yaml` triển khai API và giao diện cùng một dịch vụ Docker. Khi khởi động, dịch vụ áp dụng migration; nếu chưa có Chủ sở hữu thì tạo từ các biến môi trường được nhập riêng. `/health` chỉ báo khỏe khi kết nối database được và không còn migration chờ.
- `docker-compose.yml` định nghĩa PostgreSQL có volume/healthcheck và ứng dụng phụ thuộc database; `.dockerignore` chặn secret và bản dựng khỏi Docker context. Chưa chạy thử Compose vì môi trường thiếu Docker engine.
- `.env.example` và `docs/DEPLOYMENT.md` mô tả biến môi trường và các bước triển khai.

## Kiểm tra đã chạy

- .NET build và publish Release: đạt, còn cảnh báo từ mã khôi phục.
- Angular production build: đạt.
- PostgreSQL 16 riêng cho kiểm thử: áp dụng 7 migration thành công; khởi động với `RUN_MIGRATIONS_ON_START=true`, tạo Chủ sở hữu ban đầu, `/health` trả 200 và đăng nhập Chủ sở hữu trả 200.
- Tìm khách hàng theo tên, số điện thoại hoặc email có phân trang; màn hình khách hàng và quầy bán có ô tìm. PostgreSQL smoke test xác nhận lọc, phân trang, ký tự `%` là chữ thường, chặn tham số sai và yêu cầu đăng nhập.
- Tìm đơn hàng theo mã, trạng thái, ngày Việt Nam và người bán, có phân trang. PostgreSQL smoke test xác nhận lọc, phân trang, chặn tham số sai, chặn truy cập ẩn danh và chỉ Chủ sở hữu được lọc theo người bán.
- Sản phẩm có mã vạch, đơn vị tính và ngưỡng cảnh báo tồn đi từ form tới API/database; tìm đúng mã vạch. PostgreSQL integration smoke test xác nhận tạo, đọc, sửa và tìm mã vạch cùng các luồng nghiệp vụ cũ.
- POS mobile có nút quét mã vạch bằng camera qua plugin Capacitor; mã quét được tra trên API thực rồi thêm sản phẩm đang bán vào giỏ. Angular build và đồng bộ native Android/iOS đạt; chưa kiểm thử camera trên thiết bị thật hoặc build APK/IPA.
- Owner đặt lại mật khẩu nhân viên qua giao diện/API, thao tác được ghi Audit Log. JWT cũ bị từ chối ngay khi mật khẩu đổi hoặc tài khoản bị khóa; PostgreSQL integration smoke test đã xác nhận nhân viên không tự đặt lại qua API Owner, mật khẩu cũ và phiên cũ không còn dùng được, mật khẩu mới đăng nhập được.
- Xem và in hóa đơn từ chi tiết đơn, sản phẩm và các khoản thanh toán trong database; trình duyệt có thể lưu bản in thành PDF. PostgreSQL integration smoke test xác nhận tổng tiền, tiền đã thu, công nợ và dòng hàng. Chưa kiểm tra máy in vật lý hoặc chức năng in trên app native.
- Giao diện hiển thị trạng thái đơn, phương thức thanh toán và hành động audit bằng tiếng Việt; audit API trả tên người và đối tượng thật thay cho UUID; ngày giờ đổi sang múi giờ Việt Nam. Danh sách sản phẩm và báo cáo hiển thị thêm các trường backend đã có. Build và PostgreSQL integration smoke test đạt; chưa có kiểm thử giao diện tự động cho mọi màn hình.
- Màn Thu chi dùng API thật: ghi thu khác, chi vận hành, trả nhà cung cấp, chi khác; xem lịch sử kèm tên người ghi; giữ khóa idempotency khi kết nối gián đoạn và không cộng tiền bán hàng lần hai. PostgreSQL integration smoke test xác nhận gửi lại cùng mã không tạo phiếu trùng, doanh thu bán hàng giữ nguyên và dòng tiền cập nhật đúng.
- Màn Kho hiển thị tổng số lượng tồn, hàng sắp hết/hết hàng và lịch sử nhập, bán, trả, kiểm kho từ database. Lịch sử có tên sản phẩm, người thao tác, tồn trước/sau, lọc sản phẩm và phân trang; API chỉ trả dữ liệu khi có quyền xem kho.
- Báo cáo tách tiền đã thu, tiền đã hoàn và thực nhận theo tiền mặt, chuyển khoản, QR từ giao dịch thật; API giới hạn khoảng ngày và kiểm tra quyền xem báo cáo.
- Báo cáo 10 sản phẩm bán chạy hiển thị tên, số đã bán, đã trả và bán ròng theo khoảng ngày từ đơn hàng và phiếu trả thật.
- Sửa khởi động Render khi chưa có biến `OWNER_*`: dịch vụ ghi cảnh báo và vẫn chạy; khi nhập đủ thông tin hợp lệ rồi triển khai lại thì tạo Chủ sở hữu. PostgreSQL smoke test trên database mới xác nhận `/health` trả 200 trước khi thiết lập, đăng nhập trả 401 trước và 200 sau khi nhập thông tin.

## Chưa xác minh

- Không có mã nguồn hoặc đối tượng Git của commit `914045c` trong bản lưu hiện tại, nên không thể xác nhận mã mới trùng chức năng hoặc đúng số tệp của commit đó. Không thêm tệp chỉ để đạt một con số.
- Chưa chạy toàn bộ luồng nghiệp vụ trên PostgreSQL từ bản mã khôi phục; chưa thử Docker, Render, Neon, Android hay iOS trên thiết bị thật.
- Các tính năng được nhắc trong lịch sử sau bản dựng này, gồm báo cáo công nợ, cấu hình cửa hàng và xử lý giao dịch khi mất mạng, cần được xây lại và kiểm thử riêng.

Chưa sử dụng bản này với dữ liệu kinh doanh thật trước khi kiểm thử đầy đủ các mục trên.
