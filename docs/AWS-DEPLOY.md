# Triển khai Miêu lên AWS (Free Tier)

Kiến trúc: **một máy EC2** chạy Docker Compose gồm Caddy (HTTPS tự động) → ứng dụng (.NET + Angular cùng origin) → PostgreSQL. Dữ liệu nằm trên ổ EBS, sao lưu hằng ngày lên S3.

```
Internet ─443→ Caddy ─→ app:8080 ─→ db:5432 (không mở ra ngoài)
                                    └─ /srv/mieu/pgdata (EBS)  ──cron──▶ S3 (backup)
```

Các file liên quan nằm trong [deploy/aws/](../deploy/aws/): `docker-compose.prod.yml`, `Caddyfile`, `.env.production.example`, `bootstrap-ec2.sh`, `deploy.sh`, `backup.sh`, `restore.sh`, `iam-backup-policy.json`.

## Trước khi bắt đầu: chi phí có thật sự miễn phí không?

AWS đã đổi chính sách và các nguồn tôi tra không thống nhất, nên **hãy tự xác nhận trong tài khoản của bạn**:

- Tài khoản tạo **từ 15/07/2025**: dùng "Free plan" bằng **credit** (khoảng 100–200 USD, hết hạn sau vài tháng), không phải 750 giờ miễn phí. Máy EC2 và ổ đĩa trừ vào credit này. Khi hết credit, nếu bạn nâng lên Paid plan thì **bắt đầu tính tiền thật**.
- Tài khoản tạo **trước** mốc đó: có thể còn 750 giờ `t2/t3.micro` mỗi tháng trong 12 tháng đầu.
- Loại máy được free plan chấp nhận (theo các nguồn): `t3.micro`, `t3.small`, `t4g.micro`, `t4g.small`. Kiểm tra trong màn hình tạo EC2 xem có nhãn "Free tier eligible".
- Ước tính sau khi hết ưu đãi: `t3.small` + 20 GB EBS + Elastic IP khoảng 17–20 USD/tháng.

**Việc đầu tiên nên làm:** tạo cảnh báo ngân sách (bước 1) để không bị tính tiền bất ngờ.

Tôi khuyên dùng **`t3.small` (2 GB RAM)**. `t3.micro` (1 GB) vẫn chạy được ứng dụng nhưng build Docker rất dễ hết bộ nhớ.

## Bạn cần làm (theo thứ tự)

### 1. Tài khoản AWS và cảnh báo chi phí (10 phút)
1. Đăng ký tại aws.amazon.com, bật **MFA** cho tài khoản root.
2. Chọn một region gần Việt Nam, ví dụ **Singapore (ap-southeast-1)**. Dùng cùng region cho mọi bước dưới.
3. Vào **Billing → Budgets → Create budget**, chọn *Zero spend budget* (cảnh báo khi phát sinh chi phí) và nhập email của bạn.
4. Không dùng tài khoản root để làm việc hằng ngày; tạo user IAM hoặc dùng IAM Identity Center nếu cần.

### 2. Tên miền (5 phút)
Cần một tên miền trỏ vào máy để có HTTPS (bắt buộc: cookie đăng nhập đặt cờ `Secure`).
- **Có domain:** chuẩn bị sẵn, ở bước 4 sẽ tạo bản ghi `A` trỏ tới Elastic IP.
- **Chưa có domain (miễn phí):** dùng `sslip.io`. Nếu Elastic IP là `3.14.15.92` thì host là `3-14-15-92.sslip.io`; Let's Encrypt cấp chứng chỉ bình thường.

### 3. Bucket S3 và quyền sao lưu (10 phút)
Thay `TEN-BUCKET-CUA-BAN` bằng tên duy nhất toàn cầu (ví dụ `mieu-pos-backup-<chuoi-ngau-nhien>`), chạy bằng AWS CloudShell hoặc AWS CLI đã đăng nhập:

```bash
BUCKET=TEN-BUCKET-CUA-BAN
REGION=ap-southeast-1

aws s3api create-bucket --bucket $BUCKET --region $REGION \
  --create-bucket-configuration LocationConstraint=$REGION
aws s3api put-public-access-block --bucket $BUCKET \
  --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
aws s3api put-bucket-encryption --bucket $BUCKET \
  --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
# Tự xóa bản sao lưu cũ hơn 60 ngày
aws s3api put-bucket-lifecycle-configuration --bucket $BUCKET --lifecycle-configuration \
  '{"Rules":[{"ID":"expire-old-backups","Status":"Enabled","Filter":{"Prefix":"db/"},"Expiration":{"Days":60}}]}'

# Vai trò cho máy EC2 (không cần lưu access key trên máy)
sed "s/REPLACE_WITH_BUCKET/$BUCKET/g" deploy/aws/iam-backup-policy.json > /tmp/policy.json
aws iam create-policy --policy-name mieu-backup --policy-document file:///tmp/policy.json
cat > /tmp/trust.json <<'JSON'
{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ec2.amazonaws.com"},"Action":"sts:AssumeRole"}]}
JSON
aws iam create-role --role-name mieu-ec2 --assume-role-policy-document file:///tmp/trust.json
ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
aws iam attach-role-policy --role-name mieu-ec2 --policy-arn arn:aws:iam::$ACCOUNT:policy/mieu-backup
aws iam create-instance-profile --instance-profile-name mieu-ec2
aws iam add-role-to-instance-profile --instance-profile-name mieu-ec2 --role-name mieu-ec2
```

### 4. Tạo máy EC2 (15 phút, trên console)
1. **EC2 → Launch instance**.
2. *AMI*: **Amazon Linux 2023**. *Type*: **t3.small** (hoặc loại có nhãn Free tier eligible).
3. *Key pair*: tạo mới, tải file `.pem` về và giữ kín.
4. *Network → Security group*, tạo mới với các luật vào:
   - SSH (22): **chỉ "My IP"**. Không mở 22 cho mọi người.
   - HTTP (80) và HTTPS (443): Anywhere (cần cho Caddy cấp chứng chỉ).
5. *Storage*: **20 GiB gp3** (không bật tăng thêm).
6. *Advanced details → IAM instance profile*: chọn **mieu-ec2**. *Metadata version*: **V2 only**.
7. Launch. Sau đó **EC2 → Elastic IPs → Allocate → Associate** với máy vừa tạo. Ghi lại IP này.
8. Có domain: tạo bản ghi `A` trỏ host của bạn tới Elastic IP. Dùng sslip.io: ghi host đã quy đổi ở bước 2.

> Elastic IP đang gắn vào máy đang chạy thì không tính phí riêng; để rời hoặc gắn máy đã tắt sẽ bị tính. Xoá đi khi không dùng.

### 5. Chuẩn bị máy (10 phút)
```bash
chmod 400 ~/Downloads/KEY.pem
ssh -i ~/Downloads/KEY.pem ec2-user@ELASTIC_IP

# Trên máy EC2: lấy mã nguồn
git clone https://github.com/hodangquynh1546/HONG-BEAUTY-POS.git
bash HONG-BEAUTY-POS/deploy/aws/bootstrap-ec2.sh
exit            # thoát rồi SSH lại để nhóm docker có hiệu lực
```
Nếu repo là **private**, `git clone` sẽ hỏi tài khoản: tạo *fine-grained personal access token* chỉ có quyền **Contents: Read-only** cho repo này trên GitHub và dùng làm mật khẩu. Đừng dùng token có quyền ghi.

### 6. Điền cấu hình và chạy (10 phút)
```bash
ssh -i ~/Downloads/KEY.pem ec2-user@ELASTIC_IP
cd HONG-BEAUTY-POS/deploy/aws
cp .env.production.example .env.production
chmod 600 .env.production
nano .env.production
```
Điền các giá trị:

| Biến | Giá trị |
|---|---|
| `DOMAIN` | host ở bước 2 (không `https://`) |
| `POSTGRES_PASSWORD` | chuỗi ngẫu nhiên, chỉ chữ/số/`-`/`_` (`openssl rand -hex 24`) |
| `JWT_SECRET` | `openssl rand -base64 48` |
| `OWNER_EMAIL`, `OWNER_NAME`, `OWNER_PASSWORD` | tài khoản chủ cửa hàng; mật khẩu 14 ký tự trở lên, có hoa, thường, số, ký tự đặc biệt |
| `BACKUP_BUCKET` | tên bucket ở bước 3 |

Rồi chạy:
```bash
./deploy.sh
```
Lần đầu mất khoảng 5–10 phút vì phải build. Thành công khi thấy `Healthy.`. Mở `https://DOMAIN` và đăng nhập.

**Sau khi đăng nhập được**, sửa `.env.production`: đặt `BOOTSTRAP_OWNER_ON_START=false`, **xoá dòng `OWNER_PASSWORD`**, rồi `./deploy.sh` lần nữa.

### 7. Bật sao lưu hằng ngày (5 phút)
```bash
./backup.sh                         # chạy thử, phải thấy "Backup uploaded"
( crontab -l 2>/dev/null; echo '15 20 * * * /home/ec2-user/HONG-BEAUTY-POS/deploy/aws/backup.sh >> /srv/mieu/backups/backup.log 2>&1' ) | crontab -
```
Giờ máy là UTC; `20:15 UTC` tức 03:15 sáng giờ Việt Nam. **Hãy thử khôi phục một lần** trên máy khác hoặc bằng `./restore.sh s3://BUCKET/db/<file>` trước khi tin tưởng bản sao lưu.

## Vận hành

| Việc | Lệnh (trong `deploy/aws`) |
|---|---|
| Cập nhật bản mới | `./deploy.sh` |
| Xem log | `docker compose --env-file .env.production -f docker-compose.prod.yml logs -f --tail=100 app` |
| Khởi động lại | `docker compose --env-file .env.production -f docker-compose.prod.yml restart app` |
| Khôi phục dữ liệu | `./restore.sh s3://BUCKET/db/<file>.sql.gz` (ghi đè dữ liệu hiện tại) |
| Quên mật khẩu chủ | đặt `OWNER_EMAIL`/`OWNER_PASSWORD` tạm trong `.env.production`, rồi `docker compose --env-file .env.production -f docker-compose.prod.yml run --rm app --recover-owner-password`, sau đó xoá `OWNER_PASSWORD` |

## Bảo mật cần giữ
- `.env.production` quyền `600`, không đưa vào Git hay chat.
- Cổng 22 chỉ cho IP của bạn; IP nhà đổi thì cập nhật lại trong Security Group.
- PostgreSQL không mở cổng ra ngoài (compose không publish).
- Định kỳ `sudo dnf update -y` và `./deploy.sh` để nhận bản vá.
- Bật EBS snapshot hoặc dựa vào backup S3; thử khôi phục định kỳ.

## Khắc phục sự cố
- **Caddy không lấy được chứng chỉ:** kiểm tra bản ghi DNS đã trỏ đúng Elastic IP, cổng 80/443 đã mở trong Security Group; xem `logs caddy`.
- **Đăng nhập xong bị văng ra / không giữ phiên:** thường do chưa dùng HTTPS hoặc `DOMAIN` sai. Cookie phiên chỉ gửi qua HTTPS.
- **Build hết bộ nhớ (`Killed`):** kiểm tra `free -h` thấy swap 2 GB; nếu vẫn lỗi, nâng lên `t3.small`.
- **`/health` trả 503:** database chưa sẵn sàng hoặc migration lỗi; xem `logs app db`.

## Gỡ bỏ để dừng chi phí
EC2 → Terminate instance, Release Elastic IP, xoá EBS còn sót, và xoá bucket S3 (sau khi tải backup cần giữ).

## Giới hạn của phương án này
Một máy duy nhất nên khi máy hỏng sẽ ngừng dịch vụ cho tới khi dựng lại từ backup. Phù hợp một cửa hàng; muốn bền hơn thì chuyển database sang Amazon RDS và chạy ứng dụng sau Application Load Balancer, nhưng chi phí cao hơn đáng kể.
