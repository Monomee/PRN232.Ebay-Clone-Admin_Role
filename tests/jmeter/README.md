# JMeter Test Plans — EbayClone Admin

Performance & rate-limit test cho Admin API (task **W7-18**, dùng tiếp cho **W8-03**).

## Cấu trúc

```
tests/jmeter/
├── plans/                  # Các file .jmx (mỗi kịch bản 1 file)
│   ├── 00-login-smoke.jmx          # Kiểm tra nhanh app sống + login OK
│   ├── 01-login-bruteforce.jmx     # Brute-force login, kỳ vọng 429
│   ├── 02-dashboard-stats.jmx      # Dashboard/Stats, 100–500 users
│   ├── 03-users-orders-paging.jmx  # Users/Orders list phân trang
│   ├── 04-dispute-detail.jmx       # Dispute detail theo id từ CSV
│   ├── 10-security-ip-whitelist.jmx # W7-19: IP whitelist (giả lập IP qua X-Forwarded-For)
│   ├── 11-security-2fa.jmx          # W7-19: thiếu/sai mã 2FA không được cấp cookie
│   └── 12-security-rbac.jmx         # W7-19: phân quyền Monitor / Support / chưa đăng nhập
├── config/
│   ├── local.properties    # host/port/threads mặc định (docker-compose)
│   └── load-500.properties # profile tải cao, ghi đè threads/rampup/duration
├── data/                   # CSV data (accounts, paging, dispute-ids, ip-cases, rbac-cases)
├── scripts/
│   ├── run.ps1             # Chạy 1 plan non-GUI + xuất HTML report
│   └── run-all.ps1         # Chạy toàn bộ theo đúng thứ tự
└── results/
    ├── baseline/           # Kết quả baseline hiệu năng (W7-18) — được commit
    └── security/           # Kết quả security test (W7-19) — được commit
```

## Yêu cầu

- Java 8+ (khuyên dùng JDK 17)
- Apache JMeter **5.6.x** — đặt biến môi trường `JMETER_HOME` (vd `$env:JMETER_HOME = "E:\apache-jmeter-5.6.3"`, hoặc `setx` để lưu vĩnh viễn)
- Chạy script bằng **PowerShell** (không phải cmd)
- App chạy bằng docker-compose: `docker compose up -d --build` → API ở `http://localhost:5000`

## Kịch bản

| Plan | Endpoint | Kỳ vọng |
|---|---|---|
| 00 | `POST /api/Auth/login` | 200, `success=true`, có `Set-Cookie: auth_token` |
| 01 | `POST /api/Auth/login` (sai mật khẩu) | 5 request đầu 200 (`success=false`), request thứ 6 **429** + `Retry-After` |
| 02 | `GET /api/Dashboard/metrics`, `GET /api/Stats/dashboard`, `GET /api/Stats/reports/*` | Error % ≈ 0, ghi lại p95 & throughput |
| 03 | `GET /api/Users?PageNumber=&PageSize=&IsDescending=false`, `GET /api/Orders?PageNumber=&PageSize=` | 200, `$.pageNumber` đúng trang yêu cầu |
| 04 | `GET /api/Disputes/{id}` | 200, `$.id` khớp id yêu cầu |

## Ghi chú kỹ thuật (quan trọng)

1. **Token nằm trong cookie, không nằm trong body.** Login trả `Set-Cookie: auth_token=...; secure; httponly`.
   Cookie `secure` không được gửi qua `http://`, nên plan phải:
   - Regular Expression Extractor trên **Response Headers**: `auth_token=([^;]+)` → biến `token`
   - JSR223 PostProcessor (groovy): `props.put("token", vars.get("token"))` để chia sẻ token cho mọi thread
   - Header Manager của Thread Group chính: `Cookie: auth_token=${__P(token)}`
   - Login admin có thêm assertion `$.requireTwoFactor == false` và header chứa `auth_token=` để báo lỗi ngay nếu không lấy được token
2. **Login sai vẫn trả HTTP 200** (`success=false`) → kiểm tra bằng JSON Assertion, không dựa vào status code.
3. **Rate limit login (`strict`: 5 req/60s) là global, không tách theo IP.**
   - Kịch bản 02–04: login **một lần** trong *setUp Thread Group*, chia sẻ token qua property (xem mục 1).
   - Kịch bản 01 chạy **riêng**, sau khi đợi ≥ 60s. `run-all.ps1` đã xử lý việc này.
4. Assertion kỳ vọng 429 phải tick **Ignore Status**, nếu không JMeter tự đánh fail mọi 4xx.
5. Tài khoản test: `superadmin@ebay.local` / `Admin123!` (seed sẵn, không bật 2FA). Không dùng `admin@ebay.local` cho kịch bản 03: role Administrator bị **403** ở `/api/Users` (policy `ManageUsers` chỉ cho Support/SuperAdmin).
   **Tài khoản này phải tắt 2FA** (nếu bật, login trả `requireTwoFactor=true` và không có cookie → mọi request 401 `invalid_token`).
   Trạng thái 2FA nằm trong DB local của từng máy. Tắt bằng (chạy ở thư mục gốc project, cờ `-I` bắt buộc):
   ```powershell
   $pw = ((Get-Content .env | Where-Object { $_ -match '^SA_PASSWORD=' }) -replace '^SA_PASSWORD=', '').Trim()
   docker exec ebay_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$pw" -C -I -d CloneEbayDB -Q "UPDATE [User] SET TwoFactorEnabled = 0 WHERE Email = 'superadmin@ebay.local'"
   ```
6. `data/dispute-ids.csv` chứa id 1–11 (lấy từ DB ngày 2026-10-21). Nếu DB đã seed lại, lấy id thật rồi cập nhật file:
   ```powershell
   docker exec ebay_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$pw" -C -I -d CloneEbayDB -h -1 -W -Q "SET NOCOUNT ON; SELECT Id FROM Dispute ORDER BY Id"
   ```
7. `GET /api/Users` bắt buộc query `IsDescending` → plan 03 gửi `IsDescending=false` (xem phát hiện ở kịch bản 03).

## Tham số hoá

Trong `.jmx` dùng `${__P(tên,mặc_định)}`, ví dụ:

| Chỗ dùng | Giá trị |
|---|---|
| HTTP Request Defaults → Server / Port | `${__P(host,localhost)}` / `${__P(port,5000)}` |
| Thread Group → Threads / Ramp-up / Duration | `${__P(threads,2)}` / `${__P(rampup,5)}` / `${__P(duration,20)}` |
| CSV Data Set → Filename | `${__P(data_dir,../data)}/paging.csv` (đường dẫn tương đối so với file .jmx) |
| Body login | `${__P(admin_email,superadmin@ebay.local)}` / `${__P(admin_password,Admin123!)}` |

Giá trị mặc định trong `.jmx` cố tình nhỏ để debug trong GUI (GUI không đọc `local.properties`).
Khi chạy bằng script, `config/local.properties` được nạp → 100 users, ramp-up 30s, 2 phút.

## Chạy

GUI chỉ dùng để dựng/debug plan. Đo tải luôn chạy **non-GUI**.

```powershell
cd tests\jmeter

# 1 plan, cấu hình mặc định
.\scripts\run.ps1 -Plan 02-dashboard-stats

# 1 plan, tải cao
.\scripts\run.ps1 -Plan 02-dashboard-stats -Profile load-500

# ghi đè 1 tham số
.\scripts\run.ps1 -Plan 03-users-orders-paging -Extra "-Jthreads=200"

# toàn bộ
.\scripts\run-all.ps1
```

Lệnh JMeter tương đương (không dùng script):

```powershell
jmeter -n -t plans\02-dashboard-stats.jmx -q config\local.properties `
       -l results\run1\results.jtl -e -o results\run1\report
```

Mở `results\<plan>-<thời gian>\report\index.html` để xem report.

> Nếu PowerShell chặn script: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

## Baseline

Baseline được chạy từng plan ở 3 mức tải (100 / 200 / 500 users), sau đó thư mục kết quả được đổi tên thành `results/baseline/<plan>-<users>u/`
(mỗi thư mục gồm `results.jtl` + `report/index.html`). Trước khi chạy: `db` phải **healthy** và `00-login-smoke` phải pass.

```powershell
.\scripts\run.ps1 -Plan 04-dispute-detail                          # 100 users, 2 phút
.\scripts\run.ps1 -Plan 04-dispute-detail -Extra "-Jthreads=200"   # 200 users, 2 phút
.\scripts\run.ps1 -Plan 04-dispute-detail -Profile load-500        # 500 users, 5 phút
```

**Môi trường:** docker-compose local (1 instance API + SQL Server 2022 + Redis), commit `4983a90`, chạy ngày 2026-10-20 → 2026-10-21. JMeter 5.6.3, chạy cùng máy với Docker.

| Thông số | Giá trị |
|---|---|
| CPU |AMD Ryzen 5 4600H  |
| RAM | 16GB |
| OS | Windows 11 Pro |
| Docker Desktop (CPUs / Memory cấp cho Docker) | 12 CPUs/8020348928 bytes |

### Kịch bản 01 — Login brute-force (`results/baseline/01-login-bruteforce`)

| Request | Samples | Avg (ms) | p95 (ms) | Error % | Kết luận |
|---|---|---|---|---|---|
| Login sai — trong giới hạn | 5 | 150 | 210 | 0% | 5 lần đầu được xử lý (HTTP 200, `success=false`) |
| Login sai — vượt giới hạn | 1 | 3 | 3 | 0% | Lần 6 bị chặn **429** + `Retry-After` ✅ |

Request bị chặn chỉ mất ~3 ms vì rate limiter từ chối ở middleware, trước khi chạm DB/BCrypt.

### Kịch bản 02 — Dashboard/Stats (timer 1–3s/request)

Cột "Trang" = transaction "Mở trang Dashboard" (gồm 5 API); cột "Request" = từng API lẻ.

| Thư mục | Users | Thời gian | Trang p95 (ms) | Trang throughput (lượt/s) | Error % | Request p95 (ms) | Request throughput (req/s) |
|---|---|---|---|---|---|---|---|
| `02-dashboard-stats-100u` | 100 | 2 phút | 60 | 8.9 | 0% | 18 | 43.8 |
| `02-dashboard-stats-200u` | 200 | 2 phút | 72 | 17.8 | 0% | 21 | 87.2 |
| `02-dashboard-stats-500u` | 500 | 5 phút | 168 | 44.9 | 0% | 47 | 223.2 |

Nhận xét: throughput tăng tuyến tính theo số user, không có lỗi, p95 vẫn < 500 ms ở 500 users →
1 instance chưa chạm ngưỡng. W8-03 cần tăng tải (1000–2000 users hoặc bỏ timer) để thấy rõ tác dụng của scale-out.


### Kịch bản 03 — Users/Orders phân trang (CSV `paging.csv`, timer 1–3s/request)

| Thư mục | Users | Thời gian | Users p95 (ms) | Users throughput (req/s) | Users error % | Orders p95 (ms) | Orders throughput (req/s) | Orders error % |
|---|---|---|---|---|---|---|---|---|
| `03-users-orders-paging-100u` | 100 | 2 phút | 15 | 22.0 | 0% | 13 | 21.8 | 0% |
| `03-users-orders-paging-200u` | 200 | 2 phút | 17 | 44.2 | 0% | 14 | 43.8 | 0% |
| `03-users-orders-paging-500u` | 500 | 5 phút | 39 | 111.9 | 0% | 33 | 111.6 | 0% |

p95 theo `PageSize` (lần 500 users): Users 38 / 38 / 41 ms, Orders 33 / 33 / 34 ms với PageSize 10 / 20 / 50.

Nhận xét: throughput tăng tuyến tính, không lỗi, p95 < 50 ms. PageSize gần như không ảnh hưởng vì DB seed chỉ có vài chục bản ghi —
muốn đo tác động thật của phân trang cần seed thêm dữ liệu (hàng nghìn users/orders).

Phát hiện khi viết kịch bản:
- `GET /api/Users` bắt buộc query `IsDescending` (kiểu `bool` không nullable với `[AsParameters]`). Thiếu tham số → exception thay vì **400**. Plan gửi `IsDescending=false`.
- Môi trường Development trả stack trace đầy đủ khi lỗi (chuyển cho W7-19).

### Kịch bản 04 — Dispute detail (CSV `dispute-ids.csv`, id 1–11, timer 1–3s/request)

| Thư mục | Users | Thời gian | Avg (ms) | p95 (ms) | Throughput (req/s) | Error % |
|---|---|---|---|---|---|---|
| `04-dispute-detail-100u` | 100 | 2 phút | 44 | 77 | 43.3 | 0% |
| `04-dispute-detail-200u` | 200 | 2 phút | 63 | 108 | 84.9 | 0% |
| `04-dispute-detail-500u` | 500 | 5 phút | **927** | **2651** | **153.8** | 0% |

Diễn biến lần 500 users (theo khung 30s):

| Thời điểm | Threads đang chạy | Throughput (req/s) | p95 (ms) |
|---|---|---|---|
| 0–30s (đang ramp-up) | ≤ 261 | 61.5 | 134 |
| 30–60s | 500 | 145.2 | 1839 |
| 60–90s | 500 | 137.9 | 4193 |
| 90–300s | 500 | 150–204 | 1018–3310 |

**Đây là điểm bão hoà duy nhất tìm thấy ở baseline.** Từ ~260 users trở lên, p95 tăng từ ~100 ms lên 2–4 s và
throughput dừng ở ~150–200 req/s (lý thuyết ~250 req/s nếu server theo kịp) — request bắt đầu xếp hàng.
Error % vẫn 0 nhưng p95 vượt ngưỡng APDEX 1.5 s.

Ghi chú:
- Dispute detail nặng hơn các API khác (join order, tin nhắn, buyer/seller) → là kịch bản nên dùng để so sánh 1 vs 2–3 replica ở **W8-03**.
- Cần theo dõi CPU của container `api` và `db` (`docker stats`) khi chạy lại để biết nút thắt ở API hay SQL Server:
  nếu SQL Server là nút thắt thì thêm replica API sẽ cải thiện ít.
- `GetDisputeDetail` bắt mọi exception và trả **404**, nên lỗi phía server dưới tải sẽ hiện thành 404 thay vì 500.
  Plan có assertion `$.id == ${disputeId}` để phát hiện trường hợp này.

### Tổng kết baseline (1 instance, 500 users)

| Kịch bản | p95 (ms) | Throughput (req/s) | Error % | Trạng thái |
|---|---|---|---|---|
| 01 Brute-force | — | — | 0% | Rate limit hoạt động (429 ở request thứ 6) |
| 02 Dashboard/Stats | 47 (request lẻ) | 223.2 | 0% | Chưa bão hoà |
| 03 Users/Orders | 30 | 221.2 | 0% | Chưa bão hoà |
| 04 Dispute detail | 2651 | 153.8 | 0% | **Bão hoà từ ~260 users** |

## Security test (W7-19)

Nguyên tắc: **assertion luôn ghi hành vi đúng về bảo mật**. Ca nào đỏ = lỗ hổng tìm được (không phải lỗi của plan).
Các ca ghi dữ liệu dùng id không tồn tại (`999999`) → nếu lọt qua phân quyền chỉ nhận 404/400, không sửa dữ liệu thật.

Chạy (cách nhau ≥ 60s vì plan 11, 12 dùng lượt login):

```powershell
.\scripts\run.ps1 -Plan 10-security-ip-whitelist
.\scripts\run.ps1 -Plan 11-security-2fa       # 3 lượt strict (login + 2 verify-2fa)
.\scripts\run.ps1 -Plan 12-security-rbac      # 2 lượt strict (login monitor + support)
```

Tài khoản: `techseller@example.com` / `Password123!` (seed **bật 2FA**), `monitor@ebay.local`, `support@ebay.local` / `Admin123!` (phải **tắt 2FA**).

### Kết quả (2026-10-21, commit `4983a90`) — `results/security/`

**10 — IP whitelist** (`data/ip-cases.csv`, gửi `X-Forwarded-For` để giả lập IP client)

| Ca | IP | Kỳ vọng | Thực tế | |
|---|---|---|---|---|
| 01 | 8.8.8.8 | 403 | 403 | ✅ |
| 02 | 203.0.113.10 | 403 | 403 | ✅ |
| 03 | 172.20.0.5 | 200 | 200 | ✅ đối chứng |
| 04 | 192.168.1.50 | 200 | 200 | ✅ đối chứng |
| 05 | 100.1.2.3 | 403 | **200** | ❌ **SEC-02** |

**11 — 2FA** (tài khoản bật 2FA)

| Bước | Kỳ vọng | Thực tế | |
|---|---|---|---|
| Login đúng mật khẩu | `requireTwoFactor=true`, không có `auth_token` | đúng | ✅ |
| verify-2fa mã sai `000000` | `success=false`, không có `auth_token` | đúng | ✅ |
| verify-2fa thiếu mã | không có `auth_token` | 200, không cookie | ✅ (nên trả 400) |
| `GET /api/Auth/me` sau các bước trên | 401 | 401 | ✅ |

**12 — Phân quyền** (`data/rbac-cases.csv`)

| Ca | Role | Request | Kỳ vọng | Thực tế | |
|---|---|---|---|---|---|
| P1 | Monitor | GET /api/Dashboard/metrics | 200 | 200 | ✅ đối chứng |
| P2 | Support | GET /api/Users | 200 | 200 | ✅ đối chứng |
| M1–M4 | Monitor | PUT/POST Users status/ban, PUT Financials/fees, POST AdminRoles/roles | 403 | 403 | ✅ |
| M5 | Monitor | POST /api/Disputes/999999/assign | 403 | **404** | ❌ **SEC-03** |
| S1–S5 | Support | Financials/fees, payouts/history, Wallets, AdminRoles/roles, AdminRoles/users | 403 | 403 | ✅ |
| S6 | Support | GET /api/Financials/settlement-orders | 403 | **200** | ❌ **SEC-04** |
| A1 | Chưa đăng nhập | POST /api/AdminReturnRequests/999999/approve | 401 | **400** | ❌ **SEC-05** |

DoD W7-19: IP ngoài whitelist → 403 ✅ · thiếu/sai 2FA không có cookie ✅ · Monitor ghi / Support tài chính & admin roles → 403 ✅ (trừ các lỗ hổng dưới đây).

### Lỗ hổng tìm được

| ID | Mức độ | Mô tả | Bằng chứng | Đề xuất sửa |
|---|---|---|---|---|
| SEC-01 | **Cao** | Giả mạo IP: `UseForwardedHeaders` tin mọi nguồn (`KnownProxies/KnownIPNetworks.Clear()`), client gửi `X-Forwarded-For: 127.0.0.1` là vượt whitelist | Plan 10 ca 01–02: header giả được dùng làm IP client | Chỉ tin header từ IP của Nginx (`KnownProxies`) |
| SEC-02 | Trung bình | So khớp whitelist bằng `StartsWith` không có dấu chấm: `10.*` khớp cả `100.x`, `101.x` (IP public) | Plan 10 ca 05 | So khớp CIDR (`IPNetwork.Contains`) hoặc thêm dấu `.` |
| SEC-03 | **Cao** | `POST /api/Disputes/{id}/assign` và `/resolve` chỉ `RequireAuthorization()` → Monitor (chỉ xem) assign/resolve được dispute | Plan 12 M5: 404 (đã qua phân quyền) | `.RequireAuthorization(Policies.ManageDisputes)` |
| SEC-04 | Trung bình | `Financials/settlement-orders`, `pending/{sellerId}`, `trigger-settlement` không giới hạn role → Support xem (và có thể kích hoạt) quyết toán | Plan 12 S6: 200 | Thêm `RequireRole(Administrator, SuperAdmin)` |
| SEC-05 | **Nghiêm trọng** | `AdminReturnRequests`, `Products`, `Reviews` không có `RequireAuthorization` → người chưa đăng nhập gọi được API admin | Plan 12 A1: 400 (đã vào endpoint) | Thêm `RequireAuthorization(...)` cho cả group |
| SEC-06 | Trung bình | `verify-2fa` chỉ nhận `UserId` + `Code`, không ràng buộc với bước mật khẩu → có mã 2FA là đăng nhập được, bỏ qua mật khẩu | Đọc code `Verify2FACommand` | Cấp token tạm sau bước mật khẩu, verify-2fa bắt buộc token này |
| SEC-07 | Thấp | Môi trường Development trả stack trace đầy đủ khi lỗi | Plan 03 (thiếu `IsDescending`) | Chạy `ASPNETCORE_ENVIRONMENT=Production` khi demo/deploy |
