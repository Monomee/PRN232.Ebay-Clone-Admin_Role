# Kịch bản demo trước lớp — eBay Clone (phần Admin)

Mỗi yêu cầu trong `Requirement.txt` có một file riêng. Mỗi file gồm:
- **Mô tả trong dự án:** code nằm ở đâu và chạy thế nào.
- **Để làm gì:** lý do có chức năng này.
- **Kịch bản demo:** từng bước, kèm kết quả mong đợi.
- **Phương án dự phòng:** cách chứng minh khi demo trực tiếp bị lỗi.

## Danh sách

### Yêu cầu chính (Admin)

| # | Yêu cầu | File | Thời lượng gợi ý |
|---|---|---|---|
| 1 | Dashboard tổng quan: số user, sản phẩm, đơn hàng | [01-dashboard-tong-quan.md](01-dashboard-tong-quan.md) | 2 phút |
| 2 | Danh sách người dùng: duyệt tài khoản mới, khoá/mở khoá | [02-quan-ly-nguoi-dung.md](02-quan-ly-nguoi-dung.md) | 4 phút |
| 3 | Danh sách sản phẩm: xoá hoặc ẩn sản phẩm vi phạm | [03-quan-ly-san-pham.md](03-quan-ly-san-pham.md) | 3 phút |
| 4 | Quản lý đơn hàng toàn hệ thống | [04-quan-ly-don-hang.md](04-quan-ly-don-hang.md) | 2 phút |
| 5 | Thống kê theo ngày, tháng, quý | [05-thong-ke-ngay-thang-quy.md](05-thong-ke-ngay-thang-quy.md) | 2 phút |
| 6 | Giám sát Review + Feedback | [06-giam-sat-review-feedback.md](06-giam-sat-review-feedback.md) | 3 phút |
| 7 | Quản lý khiếu nại (Dispute): duyệt, điều phối, phân xử | [07-khieu-nai-dispute.md](07-khieu-nai-dispute.md) | 4 phút |
| 8 | Xét duyệt yêu cầu hoàn trả | [08-hoan-tra-don-hang.md](08-hoan-tra-don-hang.md) | 3 phút |
| 9 | Email cảnh báo / thông báo toàn bộ người dùng | [09-email-canh-bao-broadcast.md](09-email-canh-bao-broadcast.md) | 3 phút |
| 10 | 🔐 Admin panel chỉ từ IP nội bộ hoặc 2FA | [10-security-ip-noi-bo-va-2fa.md](10-security-ip-noi-bo-va-2fa.md) | 6 phút |
| 11 | 📉 Phân quyền rõ ràng cho từng loại admin | [11-security-phan-quyen-admin.md](11-security-phan-quyen-admin.md) | 5 phút |
| 12 | 📈 Dashboard mở rộng được | [12-dashboard-mo-rong.md](12-dashboard-mo-rong.md) | 2 phút |
| 13 | 💾 Dữ liệu nhạy cảm log riêng, mã hoá khi lưu | [13-security-du-lieu-nhay-cam.md](13-security-du-lieu-nhay-cam.md) | 5 phút |
| 14 | 🧩 Giao diện dễ dùng cho admin không chuyên | [14-giao-dien-de-dung.md](14-giao-dien-de-dung.md) | 2 phút |

### Yêu cầu khác (25% điểm)

| # | Yêu cầu | File | Thời lượng gợi ý |
|---|---|---|---|
| 15 | Load balance, Nginx | [15-load-balance-nginx.md](15-load-balance-nginx.md) | 3 phút |
| 16 | Rate limiting | [16-security-rate-limiting.md](16-security-rate-limiting.md) | 4 phút |
| 17 | CI/CD (GitHub Actions) | [17-ci-cd.md](17-ci-cd.md) | 3 phút |
| 18 | Quản lý team (Jira / ClickUp) | [18-quan-ly-team.md](18-quan-ly-team.md) | 2 phút |
| 19 | Kiểm thử tải và an ninh (JMeter) | [19-kiem-thu-tai-an-ninh-jmeter.md](19-kiem-thu-tai-an-ninh-jmeter.md) | 4 phút |
| 20 | Zero downtime khi cập nhật (K8s) | [20-zero-downtime-k8s.md](20-zero-downtime-k8s.md) | 4 phút |

### Bảo mật bổ sung (không có trong Requirement nhưng nên demo)

| # | Nội dung | File |
|---|---|---|
| S1 | Xác minh người dùng, chống tạo nhiều tài khoản (clone) | [21-security-xac-minh-nguoi-dung-chong-clone.md](21-security-xac-minh-nguoi-dung-chong-clone.md) |
| S2 | Phiên đăng nhập, khóa tài khoản, header, secret | [22-security-phien-va-cac-lop-khac.md](22-security-phien-va-cac-lop-khac.md) |

Tổng thời gian khoảng 60–70 phút. Nếu chỉ có 15–20 phút, nên chọn:
- **01 + 02:** dashboard, duyệt user (2 + 4 phút).
- **07:** dispute (4 phút).
- **10:** IP nội bộ, 2FA (6 phút).
- **11:** phân quyền (5 phút).
- **21:** chống clone, phần A–D.

---

## Chuẩn bị chung (làm trước buổi demo 1 ngày)

### 1. Chạy app ở máy local (dùng cho hầu hết các file)

```bash
docker start <container SQL Server>      # hoặc: docker compose up -d db redis
dotnet run --project src/Web --launch-profile EbayClone.Web
```

- API: `https://localhost:5001`. Swagger: `https://localhost:5001/api`.
- React: `https://localhost:44447`. SPA proxy tự bật cùng `dotnet run`. Nếu không bật thì chạy `npm start` trong `src/Web/ClientApp`.
- `src/Web/appsettings.Development.json` (không commit) phải có:
  - `"BackgroundJobs": { "Enabled": true }`: job duyệt user và monitor, cần cho file 02, 21.
  - `"EmailSettings": { "LogInsteadOfSending": true }`: mã OTP in ra console, không gửi mail thật. Muốn demo mail thật (file 09) thì đặt `false`.
  - `"GeoIp": { "DatabasePath": "..." }`: vị trí cấp thành phố, file 21.

### 2. Chạy bằng Docker Compose (file 15, 16, 19)

```bash
docker compose up -d --build
```

Mở `http://localhost:5000`. Đường đi: nginx → 2 instance `api1`, `api2` → SQL Server, Redis.

### 3. Tài khoản seed

| Email | Mật khẩu | Vai trò |
|---|---|---|
| `superadmin@ebay.local` | `Admin123!` | SuperAdmin (toàn quyền) |
| `support@ebay.local` | `Admin123!` | Support (user, sản phẩm, đơn, dispute, review, broadcast) |
| `monitor@ebay.local` | `Admin123!` | Monitor (chỉ xem dashboard và báo cáo) |
| `admin@ebay.local` | `Admin123!` | Administrator (tài chính) |
| `fashion@example.com` | `Password123!` | Seller |
| `john@example.com` | `Password123!` | Buyer |
| `pending@example.com` | `Password123!` | Seller đang chờ duyệt |

**Bật 2FA trước cho `superadmin`, `support`, `monitor`.** API bắt buộc 2FA, nên lần đăng nhập đầu sẽ bị chuyển sang `/enable2FA`. Quét QR bằng Google Authenticator, mỗi tài khoản một mục. Mã TOTP chỉ dùng được 1 lần trong 30 giây: đăng nhập 2 tab liên tiếp thì phải chờ mã mới.

**Seller và buyer cũng phải bật 2FA mới gọi được API.** Bật trước cho `fashion@example.com` và `john@example.com`, vì các file 02, 03, 09, 21 có dùng. `techseller@example.com` được seed sẵn `TwoFactorEnabled = true` nhưng không có secret, nên không đăng nhập được: đừng dùng tài khoản này.

**Đăng ký thử trên UI:**
- Mọi tài khoản tạo từ cùng một trình duyệt bị liên kết qua cookie thiết bị, +30 điểm, và rơi vào Risk Review.
- Muốn có tài khoản "sạch" thì đóng hết cửa sổ ẩn danh trước khi mở cửa sổ mới, hoặc đăng ký bằng `curl` (file 21).

Dùng 3 cửa sổ trình duyệt cho 3 vai trò: Chrome thường (SuperAdmin), Chrome Incognito (Support), Edge hoặc Firefox (Monitor). Như vậy không phải đăng xuất qua lại.

### 4. Tạo dữ liệu

Trang **Mock** (`/mock`, SuperAdmin) tạo nhanh:
- đơn hàng,
- defect,
- return request,
- dispute,
- user giả.

Chạy trước buổi demo để dashboard và các danh sách có số liệu.

### 5. Giả IP khi demo trên một máy

- App chỉ tin header `X-Forwarded-For` khi request đến từ proxy tin cậy (localhost hoặc dải private), nên `curl` từ chính máy chạy app giả được IP bất kỳ.
- `InternalIps` vẫn chặn IP lạ trước. Muốn một IP public đi qua được, thêm nó lúc chạy app. `InternalIps` trong `appsettings.json` có 6 dòng (chỉ số 0–5), nên phần tử mới là số 6:

```bash
# Git Bash
InternalIps__6=14.161.1.1 dotnet run --project src/Web --launch-profile EbayClone.Web
# PowerShell
$env:InternalIps__6="14.161.1.1"; dotnet run --project src/Web --launch-profile EbayClone.Web
```

| IP mẫu | Ý nghĩa |
|---|---|
| `14.161.1.1` | Việt Nam (Hà Nội) |
| `73.1.1.1` | Mỹ |
| `3.5.140.2` | AWS (IP datacenter) |
| `8.8.8.8` | IP lạ, không nằm trong whitelist |

### 6. Gọi API có đăng nhập từ DevTools

- Token nằm trong cookie HttpOnly, JavaScript không đọc được. Nhưng `fetch` gọi từ cùng trang vẫn tự gửi cookie.
- Cách làm: đăng nhập trên UI, mở DevTools (F12), vào tab **Console**, gọi:

```js
const r = await fetch('/api/SystemAdmin/active-ips'); r.status
```

### 7. Phương án dự phòng chung

Nếu demo trực tiếp lỗi, chạy test tự động để chứng minh hành vi:

```bash
dotnet test tests/Application.UnitTests                     # 248 test
dotnet test tests/Application.FunctionalTests               # 104 test, cần Docker (Testcontainers)
dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~Security"   # chỉ nhóm bảo mật
```

Tài liệu kỹ thuật chi tiết: `docs/Security-Hardening-2026-11.md`, `docs/SensitiveDataProtection.md`, `docs/RollingUpdate.md`, `tests/jmeter/README.md`.

### Dọn dẹp sau demo

- Xóa các tài khoản đăng ký thử.
- Gỡ (Lift) các IP block tạo khi demo.
- Bỏ biến `InternalIps__6`.
