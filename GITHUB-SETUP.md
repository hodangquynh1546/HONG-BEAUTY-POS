# Thiết lập GitHub cho Miêu

Repository chính thức: `git@github.com:hodangquynh1546/HONG-BEAUTY-POS.git`. Nhánh triển khai hiện tại là `main`. Khóa deploy `HONG-BEAUTY-POS-DEPLOY` đã được chủ sở hữu thêm với quyền ghi và đã được dùng để đẩy các commit trước đây.

Public key đã cấp:

```text
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIDqPF6pddmK04oCMEP8jn0rbcMNbMcemxjfdez0n/Fot
```

Private key nằm ngoài repository, tại `/workspace/working/keys/mieu_pos_deploy_ed25519` trong môi trường làm việc hiện tại. Không đưa private key, mật khẩu hay chuỗi kết nối vào Git hoặc tin nhắn. Trên máy khác, cần tạo khóa riêng và thêm public key mới tại Repository → Settings → Deploy keys → Add deploy key; bật **Allow write access** nếu máy đó cần đẩy mã.

Kiểm tra remote bằng `git remote -v`, kiểm tra nhánh bằng `git status -sb`. Trên máy đã cấu hình khóa, kiểm tra kết nối bằng `ssh -T git@github.com` và đẩy bằng `git push origin main`. Nếu SSH dùng khóa riêng không mặc định, cấu hình `IdentityFile` trong `~/.ssh/config` trên máy đó. Không dùng lệnh ghi đè lịch sử như force push.

Commit `914045c` và bản 175 tệp được nhắc trong lịch sử trao đổi không có trong repository hoặc bản lưu nguồn được truy cập ở môi trường này; các commit khôi phục sau đó là mã mới, không được xem là bản sao chính xác của commit đó.
