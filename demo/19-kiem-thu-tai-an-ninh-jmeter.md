# 19. Kiểm thử về tải, an ninh mạng: JMeter

## Mô tả trong dự án

| Thành phần | Vị trí |
|---|---|
| Plan | `tests/jmeter/plans/*.jmx` |
| Cấu hình | `tests/jmeter/config/local.properties` (mặc định), `load-500.properties` (tải cao) |
| Dữ liệu | `tests/jmeter/data/*.csv` (tài khoản, trang, id dispute, ca IP, ca RBAC) |
| Script | `tests/jmeter/scripts/run.ps1` (1 plan, xuất báo cáo HTML), `run-all.ps1` (chạy hết theo thứ tự, tự đợi 60 giây trước plan brute-force), `run-cluster.ps1` / `compare-cluster.ps1` (so 1 và 2 replica) |
| Môi trường | `docker-compose.loadtest.yml`: 2 replica sau nginx, bật `ALLOW_API_WITHOUT_2FA` (chỉ để đo tải; app từ chối khởi động nếu bật ở Production) |
| Kết quả đã commit | `tests/jmeter/results/baseline/` (tải), `results/security/` (an ninh) |
| Hướng dẫn | `tests/jmeter/README.md` |

### Kiểm thử tải

| Plan | Endpoint | Kỳ vọng |
|---|---|---|
| `00-login-smoke` | `POST /api/Auth/login` | 200, có `Set-Cookie: auth_token` |
| `02-dashboard-stats` | `Dashboard/metrics`, `Stats/dashboard`, `Stats/reports/*` | Lỗi khoảng 0%, ghi p95 và throughput (100–500 user) |
| `03-users-orders-paging` | `GET /api/Users`, `/api/Orders` theo trang | 200, `$.pageNumber` đúng trang yêu cầu |
| `04-dispute-detail` | `GET /api/Disputes/{id}` (id lấy từ CSV) | 200, `$.id` khớp |

### Kiểm thử an ninh

| Plan | Kiểm tra | Kỳ vọng |
|---|---|---|
| `01-login-bruteforce` | Login sai liên tục | 5 lần đầu 200 (`success=false`), lần 6 **429** kèm `Retry-After` |
| `10-security-ip-whitelist` | Giả IP qua `X-Forwarded-For` | IP ngoài whitelist bị 403, IP trong whitelist được 200 |
| `11-security-2fa` | Thiếu hoặc sai mã 2FA, gửi `2fa_challenge` giả | Không được cấp `auth_token` |
| `12-security-rbac` | Monitor, Support, khách chưa đăng nhập gọi API trái quyền | 403 / 401 đúng theo bảng quyền |

### Kết quả đã chạy (2026-11-06, JMeter 5.6.3, 2 replica, 100 thread, 2 phút mỗi plan)

| Plan | Số request | Lỗi | p95 |
|---|---|---|---|
| 02 dashboard/stats | 6 166 | 0 | 50 ms |
| 03 users/orders paging | 5 101 | 0 | 244 ms |
| 04 dispute detail | 5 023 | 0 | 390 ms |
| 11 2FA / 12 RBAC | 4 / 16 | 0 | |
| 10 IP whitelist | 5 | 1 * | |
| 01 brute-force | 6 | 1 * | |

\* Hai lỗi này được giải thích, không phải lỗi bảo mật:
- **Plan 10, ca 03:** máy chạy chỉ whitelist `172.24.0.1`, không phải cả dải `172.x`. Dữ liệu test và cấu hình `InternalIps` của máy phải khớp nhau. App chặn đúng theo cấu hình.
- **Plan 01:** với 2 replica, rate limit của app tính riêng từng replica, nên request thứ 6 chưa bị 429 (thực tế là 5 × 2). Chạy 1 replica thì có 429. Lớp chặn chung là nginx (`limit_req`) và khóa theo tài khoản (Redis). File cấu hình nginx dùng cho load test đã bỏ `limit_req` để đo tải. Xem file 16.

## Để làm gì

- **Tải:** biết hệ thống chịu được bao nhiêu người cùng lúc trước khi người dùng thật phát hiện ra. Tìm endpoint chậm (dispute detail chậm nhất, p95 390 ms).
- **An ninh:** tự động hóa các tấn công cơ bản (brute-force, giả IP, bỏ qua 2FA, vượt quyền). Mỗi lần sửa code thì chạy lại để chắc không mở lỗ hổng cũ.

## Kịch bản demo

> Cần Java 17 và JMeter 5.6.x, biến `JMETER_HOME`. Chạy trong PowerShell.

1. Khởi động môi trường đo tải:
   ```powershell
   docker compose -f docker-compose.yml -f docker-compose.loadtest.yml up -d --build
   ```
2. **Plan tải** (khoảng 2 phút). Script dùng đường dẫn tương đối, nên phải đứng trong `tests\jmeter`:
   ```powershell
   cd tests\jmeter
   .\scripts\run.ps1 -Plan 02-dashboard-stats
   ```
   Trong lúc chờ:
   - mở JMeter GUI với plan đó, chỉ vào Thread Group (100 thread, ramp-up), setUp Thread Group (login 1 lần, chia sẻ token), các assertion;
   - giải thích: token nằm trong cookie HttpOnly, nên plan dùng Regex Extractor lấy `auth_token` từ header (README mục ghi chú kỹ thuật).
3. Mở báo cáo HTML vừa sinh (`tests/jmeter/results/<plan>-<giờ chạy>/index.html`):
   - bảng **Statistics**: số request, lỗi %, p95, throughput;
   - biểu đồ **Response Time Over Time**.
4. **Plan an ninh**, chạy nhanh từng cái:
   ```powershell
   .\scripts\run.ps1 -Plan 12-security-rbac
   .\scripts\run.ps1 -Plan 11-security-2fa
   ```
   Mở `View Results Tree`:
   - Monitor gọi `/api/Users` → 403 ✅;
   - không có `2fa_challenge` → không có `auth_token` ✅.
5. **Brute-force** (chạy riêng, cách lượt trước ít nhất 60 giây, với 1 replica để thấy 429 ở lớp app):
   ```powershell
   .\scripts\run.ps1 -Plan 01-login-bruteforce
   ```
6. Nếu không đủ thời gian: mở thẳng báo cáo đã commit trong `tests/jmeter/results/baseline/` và `results/security/`.

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Sao phải bật `ALLOW_API_WITHOUT_2FA`? | JMeter không tự sinh được mã TOTP cho mỗi thread. Cờ này chỉ dùng trong môi trường đo tải, và app **từ chối khởi động** nếu bật ở Production |
| 500 user thì sao? | Dùng profile `config/load-500.properties` (ghi đè số thread, ramp-up, thời gian) |
