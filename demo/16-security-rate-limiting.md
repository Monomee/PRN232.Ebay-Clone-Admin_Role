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

## Demo trên bản deploy: https://ebay.petlify.xyz

Đường đi: Cloudflare → Caddy → `ebay_nginx_lb` (`nginx/nginx.vps.conf`, cùng 2 zone ở trên) → `api1`. Các lệnh dưới chạy được từ bất kỳ máy nào có Git Bash / Linux / macOS.

### Demo A: nginx chặn flood ở cửa vào (30 giây)

Gửi 80 request cùng lúc vào một endpoint vô hại:

```bash
seq 1 80 | xargs -P 80 -I{} curl -s -D - -o /dev/null https://ebay.petlify.xyz/api/Auth/captcha-config \
  | grep -iE "^HTTP/|x-ratelimit-source" | sort | uniq -c
```

Kết quả đã chạy ngày 2026-11-09:

```
     71 HTTP/1.1 200 OK
      9 HTTP/1.1 429 Too Many Requests
      9 x-ratelimit-source: Nginx-Gateway
```

- 25 req/giây + burst 20 được qua, phần vượt bị nginx trả **429** ngay, không đến được app.
- Xem body và `Retry-After` của một request bị chặn:
  ```bash
  seq 1 60 | xargs -P 60 -I{} curl -s -i https://ebay.petlify.xyz/api/Auth/captcha-config | grep -m1 -A12 "429"
  ```

### Demo B: chặn brute-force login (1 phút)

```bash
for i in $(seq 1 10); do
  curl -s -o /dev/null -D - https://ebay.petlify.xyz/api/Auth/login -H "Content-Type: application/json" \
    -d '{"email":"nobody@test.local","password":"sai"}' | grep -iE "^HTTP/|x-ratelimit-source" | tr '\n' ' '; echo
done
# 6–7 lần đầu: HTTP/1.1 200   (login sai vẫn trả 200, success=false)
# sau đó:     HTTP/1.1 429  x-ratelimit-source: Nginx-Gateway  (hoặc App-RateLimiter)
```

### Demo C: khóa theo tài khoản, đổi IP cũng không lách được (khoảng 80 giây, chạy bằng script)

Lớp này tính theo **email** (Redis). Dùng một tài khoản phụ có mật khẩu đã biết, **không dùng superadmin**, vì tài khoản bị khóa thật 15 phút.

```bash
./scripts/demo-lockout.sh <email-tai-khoan-phu>                    # mặc định https://ebay.petlify.xyz
./scripts/demo-lockout.sh <email> http://localhost:5000            # chạy local
```

Script hỏi mật khẩu đúng (không hiện lên màn hình), rồi:
1. gửi 10 lần sai mật khẩu. Gặp 429 thì tự chờ theo `Retry-After` rồi gửi lại, nên demo luôn **cả 2 lớp**: rate limit theo IP chặn trước, khóa theo email chặn sau;
2. đăng nhập **đúng mật khẩu** → *"Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau 15 phút."*;
3. in lệnh gỡ khóa.

Kết quả đã chạy ngày 2026-11-09 (email không tồn tại, vẫn bị đếm), mất 77 giây:

```
Lần 1/10 sai mật khẩu: Email hoặc mật khẩu không đúng
...
Lần 7/10 sai mật khẩu: Email hoặc mật khẩu không đúng
    429 do Nginx-Gateway chặn (rate limit theo IP), chờ 10s rồi gửi lại...
Lần 8/10 sai mật khẩu: Email hoặc mật khẩu không đúng
    429 do App-RateLimiter chặn (rate limit theo IP), chờ 60s rồi gửi lại...
Lần 9/10 sai mật khẩu: Email hoặc mật khẩu không đúng
Lần 10/10 sai mật khẩu: Email hoặc mật khẩu không đúng

>>> Đăng nhập với mật khẩu ĐÚNG:
    Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.
```

Gỡ khóa sau demo, trên VPS (script in sẵn lệnh này):

```bash
docker exec ebay_redis redis-cli DEL "ebay-count:login-fail:<email viết thường>"
```

### Không demo được trên bản deploy

- **Demo 2 (giả IP bằng `X-Forwarded-For`)**: Cloudflare và Caddy ghi đè header này, nên không giả IP từ bên ngoài được (đây cũng là điều mong muốn). Demo phần này ở local.

### Demo D: chỉ máy spam bị chặn, người khác vẫn dùng được (1 phút)

1. Laptop chạy flood ở Demo A với 140 request. Kết quả ngày 2026-11-09: 65 × 200, 75 × 429 (61 do `App-RateLimiter`, 14 do `Nginx-Gateway`).
2. Ngay sau đó, mở https://ebay.petlify.xyz trên **điện thoại dùng 4G** (tắt wifi): vào bình thường. Đã kiểm ngày 2026-11-09.
3. Kết luận: bộ đếm tính theo IP của từng người, không phải chung cho cả trang.

### IP thật của người dùng (đã sửa)

Trước khi sửa, đo ngày 2026-11-09: gửi 140 request vào `captcha-config` trong chưa đầy 1 phút, từ cùng một máy, mà lớp app (`standard`, 60 req/phút/IP) không chặn lần nào. Nghĩa là app không thấy IP thật của client:

- **nginx** đếm theo `$binary_remote_addr`, tức là IP của Caddy. **Mọi người dùng chung một bộ đếm**: giới hạn login 12 req/phút là của cả trang, không phải của từng người. Kiểm bằng `docker logs ebay_nginx_lb --tail 20`: cột đầu tiên là cùng một IP `172.x`.
- **App** lần ngược `X-Forwarded-For` qua các proxy tin cậy (`172.16.0.0/12`), rồi dừng ở IP edge của Cloudflare. Bộ đếm của app vì vậy tính theo IP edge Cloudflare, không theo người dùng.
- Lớp 3 (khóa theo email) **không bị ảnh hưởng**.

Cách sửa:
1. **(Đã sửa trong repo)** `nginx/nginx.vps.conf`, trong `http {}`: `set_real_ip_from 172.16.0.0/12; real_ip_header X-Forwarded-For; real_ip_recursive on;`. nginx lấy IP từ `X-Forwarded-For` mà Caddy gửi, thay vì IP của Caddy. Request gọi thẳng `http://<IP_VPS>:5000` từ Internet không nằm trong `172.16.0.0/12`, nên không giả IP được.
2. **(Cần làm trên VPS)** Caddy, trong khối `ebay.petlify.xyz`: `reverse_proxy 127.0.0.1:5000 { header_up X-Forwarded-For {http.request.header.CF-Connecting-IP} }`. Cloudflare gửi IP thật trong `CF-Connecting-IP`. Chỉ an toàn khi origin chỉ nhận kết nối từ Cloudflare. Chưa có bước này thì nginx và app đếm theo IP edge của Cloudflare: đỡ hơn một bộ đếm chung cho cả trang, nhưng chưa tính theo từng người dùng. App tự đọc đúng IP nhờ `TrustedProxyNetworks`, không phải sửa code.

Sau khi deploy bản sửa, đo lại ngày 2026-11-09: app chặn sau khoảng 60 + 5 request (đúng policy `standard`), còn điện thoại 4G không bị ảnh hưởng (Demo D).

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
