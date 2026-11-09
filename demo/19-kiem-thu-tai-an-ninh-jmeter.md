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

## Demo trên bản deploy: https://ebay.petlify.xyz

### Khác gì so với chạy local

| Ở local (`docker-compose.loadtest.yml`) | Ở bản deploy |
|---|---|
| 2 replica, bật `ALLOW_API_WITHOUT_2FA` | 1 instance, **2FA bắt buộc** (`Security__AllowApiWithout2FA=false`) |
| `limit_req` của nginx đã tắt để đo tải | nginx giới hạn **25 req/giây** cho `/api/`, cả trang dùng chung một bộ đếm (file 16) |
| Máy riêng | VPS 2 core / 4 GB chạy chung Petlify, có Cloudflare đứng trước |

Hệ quả:
- Plan **02, 03, 04** không chạy được trên bản deploy: bước login trong setUp nhận `requireTwoFactor=true`, không có cookie, nên mọi request trả 401. **Số liệu tải lấy từ môi trường local** (bảng kết quả ở trên).
- Tải lên `/api/` từ một máy chủ yếu đo **rate limiter** (nhiều 429), không đo sức chịu của app. Đây là điều đáng demo: hệ thống tự bảo vệ khi bị dồn tải.
- **Không chạy 100–500 thread lên bản deploy**: có thể làm sập cả Petlify, và Cloudflare có thể chặn IP máy test. Giữ ≤ 20 thread, ≤ 60 giây.

Mọi plan đều đọc `protocol`, `host`, `port` từ property, nên chỉ cần thêm:

```powershell
cd tests\jmeter
$prod = "-Jprotocol=https -Jhost=ebay.petlify.xyz -Jport=443"
```

### Demo A: an ninh, plan 11 (2FA) và plan 01 (brute-force) (2 phút)

```powershell
# Tài khoản thật trên bản deploy, đã bật 2FA
.\scripts\run.ps1 -Plan 11-security-2fa -OutDir results\prod -Extra "$prod -Jadmin_email=<email> -Jadmin_password=<mat-khau>"

# Brute-force: chờ ít nhất 60 giây sau lượt login trước. Mọi người dùng không đăng nhập được khoảng 30 giây (file 16, Demo B)
.\scripts\run.ps1 -Plan 01-login-bruteforce -OutDir results\prod -Extra $prod
```

- Plan 11: đúng mật khẩu nhưng chưa qua 2FA thì **không có `auth_token`**; mã 2FA sai cũng không có.
- Plan 01: request vượt giới hạn trả **429** kèm `Retry-After`. Trên bản deploy, lớp chặn thường là nginx (1 + burst 6 = 7 lần qua được). Nếu assertion ở request thứ 6 báo đỏ, chạy lại với `-Jbruteforce_allowed=7`.
- Plan 10 (IP whitelist) và 12 (RBAC) không áp dụng: bản deploy whitelist `0.0.0.0/0`, và tài khoản Monitor/Support phải qua 2FA. Thay plan 12 bằng kiểm tra khách chưa đăng nhập:
  ```bash
  curl -s -o /dev/null -w "%{http_code}\n" https://ebay.petlify.xyz/api/Users   # 401
  ```

### Demo B: tải nhẹ để thấy rate limiter hoạt động dưới tải (3 phút, JMeter GUI)

1. Mở JMeter GUI, tạo plan mới:
   - **Thread Group**: 20 thread, ramp-up 10 giây, chạy 60 giây (tick *Specify Thread lifetime*).
   - **HTTP Request Defaults**: Protocol `https`, Server `ebay.petlify.xyz`, Port `443`.
   - 2 **HTTP Request**: `GET /` (trang SPA, không bị rate limit) và `GET /api/Auth/captcha-config`.
   - Listener: **Summary Report** và **Response Time Graph**.
2. Bấm Run, chỉ vào Summary Report:
   - `GET /`: lỗi 0%, thời gian phản hồi ổn định → trang vẫn phục vụ được 20 người dùng cùng lúc.
   - `GET /api/Auth/captcha-config`: có một phần **429**, nghĩa là nginx đã cắt phần vượt 25 req/giây. Mở View Results Tree, xem header `X-RateLimit-Source: Nginx-Gateway`.
3. Kết luận: dồn tải không làm sập app, phần vượt bị chặn ở cửa vào. Số liệu năng lực (p95, throughput với 100 user) lấy từ bảng chạy local ở trên.

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Sao phải bật `ALLOW_API_WITHOUT_2FA`? | JMeter không tự sinh được mã TOTP cho mỗi thread. Cờ này chỉ dùng trong môi trường đo tải, và app **từ chối khởi động** nếu bật ở Production |
| 500 user thì sao? | Dùng profile `config/load-500.properties` (ghi đè số thread, ramp-up, thời gian) |
