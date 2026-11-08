# 16. Hạn chế số lượt request trong một khoảng thời gian: Rate limiting

Dự án có **3 lớp** giới hạn. Mỗi lớp chặn một kiểu tấn công khác nhau:

| Lớp | Nơi | Tính theo | Chặn cái gì |
|---|---|---|---|
| 1. Gateway | nginx `limit_req` | IP, **chung mọi instance** | Flood, brute-force ở cửa vào |
| 2. Ứng dụng | ASP.NET Core `RateLimiter` | IP hoặc user, **từng instance** | Lạm dụng từng loại endpoint, kể cả khi không có nginx |
| 3. Theo tài khoản (account lockout) | `SecurityCache` (Redis) | **Email**, trên mọi IP | Brute-force phân tán (botnet đổi IP liên tục) |

## Mô tả trong dự án

### Lớp 1: nginx (`nginx/nginx.conf`, bản k8s `k8s/nginx-config.yaml`)

| Zone | Áp cho | Giới hạn |
|---|---|---|
| `login_gateway_zone` | `/api/Auth/(login\|verify-2fa\|register\|verify-email\|resend-email-code)` | 12 req/phút/IP, burst 6 |
| `api_gateway_zone` | `/api/` | 25 req/giây/IP, burst 20 |

Khi vượt giới hạn, nginx trả **429** với:
- body JSON `{"error": "Too Many Requests", "source": "Nginx Load Balancer", ...}`;
- header `Retry-After: 10`;
- header `X-RateLimit-Source: Nginx-Gateway`.

### Lớp 2: ứng dụng (`src/Web/Infrastructure/RateLimitingExtensions.cs`)

| Policy | Loại | Giới hạn | Áp cho |
|---|---|---|---|
| `strict` | Fixed window, theo IP | **5 req / 60 giây**, không xếp hàng | `login`, `verify-2fa`, `register`, `verify-email`, `resend-email-code`, ... |
| `standard` | Fixed window, theo IP | 60 req / 60 giây, hàng đợi 5 | `captcha-config`, nhóm `Products`, report sản phẩm |
| `authenticated` | Sliding window (6 đoạn × 10 giây), **theo UserId** | 200 req / 60 giây | `me`, `enable-2fa`, ... |

- Khi vượt giới hạn: **429**, header `Retry-After` (số giây), `X-RateLimit-Source: App-RateLimiter`, body `{"error":"Too many requests...","source":"Application Layer"}`.
- **Trước 2026-11-05**, bộ đếm là **một bộ chung cho cả hệ thống**: 1 kẻ tấn công spam là chặn luôn mọi người. Đã sửa thành theo IP hoặc user.
- IP lấy từ `X-Forwarded-For` **chỉ khi** request đến từ proxy tin cậy. Kẻ gian không tự đổi IP bằng header để lách được.

### Lớp 3: khóa theo tài khoản (`AuthEndpoints.cs`, `SecurityCache`)

- Sai mật khẩu **10 lần** hoặc sai 2FA **5 lần** trong 15 phút: khóa **15 phút**.
- Thông báo: *"Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau 15 phút."*
- Đếm trên **mọi IP**: botnet 1.000 IP, mỗi IP thử 1 lần, vẫn bị khóa sau 10 lần.
- Email không tồn tại cũng bị đếm, nên không dò được email nào có tài khoản.

## Để làm gì

- **Chống brute-force mật khẩu:** không có giới hạn thì một máy thử được hàng nghìn mật khẩu mỗi phút.
- **Chống spam đăng ký** tài khoản clone, và spam gửi OTP email (tốn tiền SMTP, bị Gmail khóa).
- **Chống DoS nhẹ:** một client không làm nghẽn server cho những người khác.
- **Ba lớp bù cho nhau:**
  - lớp 2 tính riêng từng instance (2 instance thì thực tế là 5 × 2 lần);
  - lớp 1 (nginx) tính chung mọi instance;
  - lớp 3 chặn cả kẻ đổi IP liên tục.

---

## Kịch bản demo

### Demo 1: rate limit ở ứng dụng (1 phút), chạy local, không qua nginx

```bash
for i in $(seq 1 7); do
  curl -k -s -o /dev/null -w "lần $i → %{http_code}\n" https://localhost:5001/api/Auth/login \
    -H "Content-Type: application/json" -d '{"email":"nobody@test.local","password":"sai"}'
done
# lần 1..5 → 200   (login sai vẫn trả 200, success=false)
# lần 6, 7 → 429
```

Xem chi tiết request bị chặn:

```bash
curl -k -s -i https://localhost:5001/api/Auth/login -H "Content-Type: application/json" \
  -d '{"email":"nobody@test.local","password":"sai"}' | grep -iE "^HTTP|retry-after|x-ratelimit|error"
# HTTP/1.1 429 Too Many Requests
# Retry-After: 4x
# X-RateLimit-Source: App-RateLimiter
# {"error": "Too many requests. Please slow down...", "source": "Application Layer"}
```

### Demo 2: giới hạn tính theo IP, không phải toàn hệ thống (1 phút)

> Cần chạy app với `InternalIps__6=14.161.1.1` và `InternalIps__7=73.1.1.1` (xem README mục 5).

Ngay sau Demo 1 (IP localhost đang bị chặn), giả làm một người dùng khác:

```bash
curl -k -s -o /dev/null -w "%{http_code}\n" -H "X-Forwarded-For: 14.161.1.1" https://localhost:5001/api/Auth/login \
  -H "Content-Type: application/json" -d '{"email":"nobody@test.local","password":"sai"}'
# → 200: người khác không bị vạ lây
```

- Kẻ spam bị chặn, người dùng thật vẫn đăng nhập bình thường.
- Test tương ứng: `LoginRateLimit_IsPerClientIp`.

### Demo 3: botnet đổi IP vẫn bị khóa tài khoản (2 phút)

Dùng một tài khoản phụ có mật khẩu đã biết, ví dụ `john@example.com` (**không dùng superadmin**). Gửi 10 lần sai mật khẩu từ 2 "IP" khác nhau, 5 lần mỗi IP, để không chạm giới hạn 5 lần/IP:

```bash
for ip in 14.161.1.1 73.1.1.1; do for i in 1 2 3 4 5; do
  curl -k -s -H "X-Forwarded-For: $ip" https://localhost:5001/api/Auth/login -H "Content-Type: application/json" \
    -d '{"email":"john@example.com","password":"sai-mat-khau"}' | head -c 120; echo
done; done
```

- Chờ 60 giây (hết cửa sổ rate limit), rồi đăng nhập **đúng mật khẩu** trên giao diện.
- Kết quả: *"Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau 15 phút."*
- Kết luận: đổi IP không giúp được kẻ tấn công. Khóa tính trên tài khoản.
- Gỡ khóa sau demo:
  - nếu có Redis: `redis-cli KEYS "ebay-count:*"` rồi `DEL` key `login-fail` của email đó;
  - nếu không có Redis: restart app.

### Demo 4: rate limit ở nginx, chung mọi instance (1 phút), chạy Docker

```bash
for i in $(seq 1 20); do
  curl -s -o /dev/null -w "%{http_code} " http://localhost:5000/api/Auth/login \
    -H "Content-Type: application/json" -d '{"email":"x@test.local","password":"sai"}'
done; echo
# 200 200 ... 429 429 ...

curl -s -i http://localhost:5000/api/Auth/login -H "Content-Type: application/json" -d '{"email":"x@test.local","password":"sai"}' | grep -iE "x-ratelimit-source|source"
# X-RateLimit-Source: Nginx-Gateway   hoặc App-RateLimiter (tùy lớp nào chặn trước)
```

- Giải thích: có 2 instance, nên lớp app cho tới 5 × 2 = 10 lần. nginx đếm chung nên chặn đúng ở cửa vào.
- Header `X-RateLimit-Source` cho biết lớp nào đã chặn.

### Demo 5: chạy bằng JMeter (tùy chọn)

Plan `tests/jmeter/plans/01-login-bruteforce.jmx` (file 19):
- 5 request đầu trả 200;
- request thứ 6 trả **429** kèm `Retry-After`.

---

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Kẻ gian cố tình khóa tài khoản admin (nhập sai 10 lần)? | Có thể. Đây là đánh đổi có chủ ý: cửa sổ khóa chỉ 15 phút. Admin chờ, hoặc xóa key trong Redis. Ghi trong tài liệu (`docs/Security-Hardening-2026-11.md`, mục 2, ghi chú 14) |
| Sao login sai vẫn trả 200? | Không cho biết email có tồn tại hay không. Status code giống nhau, kết quả nằm trong `success=false` |
| Nhiều người dùng chung wifi hoặc 4G (CGNAT) thì sao? | Giới hạn tính theo IP có thể ảnh hưởng nhóm dùng chung IP. Vì vậy giới hạn chung (60/phút) đặt rộng; chỉ các endpoint nhạy cảm mới là 5/phút |

## Dự phòng

```bash
dotnet test tests/Application.FunctionalTests --filter "LoginRateLimit_IsPerClientIp|Login_LocksAccountAfterRepeatedFailures_FromAnyIp"
```
