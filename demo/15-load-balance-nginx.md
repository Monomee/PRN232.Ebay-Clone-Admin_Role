# 15. Đáp ứng khi lượng người dùng tăng lên: Load balance, Nginx

## Mô tả trong dự án

```
                 ┌──────────────── nginx (cổng 5000 / 80) ────────────────┐
Trình duyệt ───▶ │ upstream backend_api (round-robin, max_fails=3/10s)    │
                 │ limit_req: auth 12 req/phút/IP, API 25 req/giây/IP     │
                 └──────────┬───────────────────────────┬─────────────────┘
                            ▼                           ▼
                       api1:8080                    api2:8080      (ASP.NET Core, image giống nhau)
                            └──────────┬────────────────┘
                      SQL Server (dùng chung)   Redis (SignalR backplane, thu hồi token, khóa job, đếm lockout)
```

| Thành phần | Vị trí |
|---|---|
| Docker Compose | `docker-compose.yml`: `db`, `redis`, `api1`, `api2`, `nginx` |
| Cấu hình nginx | `nginx/nginx.conf` |
| Bản k8s | `k8s/nginx-config.yaml` + `k8s/nginx-deployment.yaml`, `k8s/api-deployment.yaml` (2 replica, file 20) |

Cấu hình nginx:
- `upstream backend_api` gồm `api1`, `api2`. Một instance lỗi 3 lần trong 10 giây thì bị bỏ ra 10 giây.
- Header `X-Backend-Server` cho biết request rơi vào instance nào.
- `/hubs/` hỗ trợ WebSocket cho SignalR (header `Upgrade`).
- `/nginx-health` trả 200 và không bị rate limit.

Trạng thái chung giữa các instance (để chạy được nhiều instance):
- **Redis backplane** cho SignalR: user nối vào api1 vẫn nhận event phát từ api2.
- **Token đã logout** lưu trong Redis: đăng xuất ở api1 thì api2 cũng từ chối token đó.
- **Khóa job** `IRunOnceGuard` (Redis): mỗi lượt duyệt user hoặc monitor chỉ một instance chạy.
- **Đếm sai mật khẩu** trong Redis, tính chung cho mọi instance.
- **Data Protection key** dùng chung (k8s secret), nên cookie do instance này cấp thì instance kia đọc được.

## Để làm gì

- **Mở rộng ngang (scale out):** người dùng tăng thì thêm instance API, không phải mua máy to hơn.
- **Chịu lỗi:** một instance chết thì nginx chuyển hết sang instance còn lại. Người dùng không thấy lỗi.
- **Một cửa vào:** nginx là nơi đặt rate limit chung, TLS, header bảo mật.

## Kịch bản demo

> `InternalIps` phải có gateway mạng Docker của máy chạy demo. Kiểm bằng `docker network inspect ebay_clone_adminrole_default` (tên mạng có thể khác), nếu không mọi request bị 403.

### Demo 1: hệ thống đang chạy 2 instance (30 giây)

```bash
docker compose up -d --build
docker compose ps        # thấy ebay_api_1, ebay_api_2, ebay_nginx_lb, ebay_sqlserver, ebay_redis
```

### Demo 2: round-robin, xem request rơi vào đâu (1 phút)

```bash
for i in 1 2 3 4 5 6; do
  curl -s -o /dev/null -D - http://localhost:5000/api/Auth/captcha-config | grep -i x-backend-server
done
# X-Backend-Server: 172.x.0.4:8080
# X-Backend-Server: 172.x.0.5:8080
# X-Backend-Server: 172.x.0.4:8080 ...   (2 địa chỉ luân phiên)
```

Hoặc mở `http://localhost:5000` trên trình duyệt, DevTools > Network, xem response header `X-Backend-Server` của các request `/api/...`.

### Demo 3: tắt một instance mà hệ thống vẫn chạy (1,5 phút)

1. Đăng nhập trang admin qua `http://localhost:5000` và mở Dashboard.
2. Chạy:
   ```bash
   docker stop ebay_api_1
   ```
3. Refresh Dashboard, bấm qua lại các trang. **Vẫn chạy.**
4. Chạy lại vòng `curl` ở Demo 2. Giờ `X-Backend-Server` chỉ còn 1 địa chỉ.
5. **Vẫn đăng nhập**, không bị đẩy ra. JWT do api1 cấp vẫn được api2 chấp nhận (cùng khóa ký, phiên kiểm qua DB và Redis).
6. Bật lại:
   ```bash
   docker start ebay_api_1
   ```
   Sau vài giây, `X-Backend-Server` luân phiên trở lại.

### Demo 4: realtime xuyên instance (1 phút, tùy chọn)

1. Mở cùng một dispute ở 2 cửa sổ. Hai kết nối SignalR có thể nằm ở 2 instance khác nhau.
2. Cửa sổ 1 bấm **Assign to me**. Cửa sổ 2 vẫn cập nhật ngay, nhờ Redis backplane chuyển event giữa các instance.

### Demo 5: số liệu tải (30 giây)

- Chỉ vào bảng kết quả JMeter trong `docs/Security-Hardening-2026-11.md` mục 1.13 (2 replica sau nginx, 100 thread):
  - dashboard: p95 khoảng 50 ms, 0 lỗi;
  - dispute detail: p95 khoảng 390 ms.
- Chi tiết: file 19.

## Demo trên bản deploy: https://ebay.petlify.xyz

### Kiến trúc thật trên VPS

```
Trình duyệt ─▶ Cloudflare ─▶ Caddy (host VPS, chung với Petlify) ─▶ ebay_nginx_lb :5000 ─▶ api1:8080
               (TLS, CDN)     (reverse proxy theo domain)            (nginx/nginx.vps.conf)    (1 instance)
```

- VPS chỉ có 2 core / 4 GB và chạy chung Petlify, nên `docker-compose.vps.yml` chỉ chạy **1 instance** (`api1`, giới hạn 450 MB). nginx vẫn đứng trước: rate limit, WebSocket SignalR, `/nginx-health`. Muốn scale thì chỉ cần thêm dòng `server` vào `upstream`.
- Bản 2 instance đầy đủ chạy local bằng `docker-compose.yml` (Demo 1–4 ở trên).

### Demo A: request đi qua từng lớp (30 giây)

```bash
curl -s -o /dev/null -D - https://ebay.petlify.xyz/api/Auth/captcha-config | grep -iE "^server|^via|x-backend-server"
# Server: cloudflare                 <- lớp Cloudflare
# via: 1.1 Caddy                     <- reverse proxy trên VPS
# x-backend-server: 172.20.0.4:8080  <- nginx của eBay chọn instance api1
```

Trên trình duyệt: mở https://ebay.petlify.xyz, DevTools > Network, chọn một request `/api/...`, xem 3 header trên.

```bash
curl -s https://ebay.petlify.xyz/nginx-health    # healthy
```

### Demo B: bật tạm instance thứ 2 trên VPS để thấy round-robin và failover (5 phút, tùy chọn)

> Tốn thêm khoảng 450 MB RAM. Trước khi làm, xem `free -m` trên VPS còn trống ít nhất 600 MB. Làm xong thì tắt đi.

Trên VPS, trong `/opt/ebay-admin`:

1. Thêm `api2` vào `docker-compose.vps.yml`: chép nguyên khối `api1`, đổi 2 dòng:
   ```yaml
     api2:
       container_name: ebay_api_2
       ...
       environment:
         ...
         BackgroundJobs__Enabled: "false"   # job nền chỉ chạy ở api1
   ```
2. Thêm `api2` vào `upstream` trong `nginx/nginx.vps.conf`:
   ```nginx
   upstream backend_api {
       server api1:8080 max_fails=3 fail_timeout=10s;
       server api2:8080 max_fails=3 fail_timeout=10s;
       keepalive 32;
   }
   ```
3. Chạy:
   ```bash
   docker compose -f docker-compose.vps.yml up -d api2
   docker exec ebay_nginx_lb nginx -s reload
   ```
4. Từ máy bất kỳ:
   ```bash
   for i in 1 2 3 4 5 6; do
     curl -s -o /dev/null -D - https://ebay.petlify.xyz/api/Auth/captcha-config | grep -i x-backend-server
   done
   # 2 địa chỉ 172.20.0.x:8080 luân phiên
   ```
5. Failover: trên VPS chạy `docker stop ebay_api_1`, rồi refresh trang admin. Vẫn dùng được, vẫn đăng nhập, và `x-backend-server` chỉ còn 1 địa chỉ. Chạy `docker start ebay_api_1` để bật lại.
6. Dọn dẹp: `docker compose -f docker-compose.vps.yml rm -sf api2`, xóa dòng `api2` trong `upstream`, rồi `docker exec ebay_nginx_lb nginx -s reload`.

Hai instance dùng chung SQL Server và Redis có sẵn trên VPS, và cùng `Jwt__Key`, nên JWT do api1 cấp vẫn được api2 chấp nhận.

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Sao bản deploy chỉ có 1 instance? | VPS 2 core / 4 GB chạy chung Petlify. Kiến trúc đã sẵn sàng chạy nhiều instance (stateless, Redis, nginx upstream); thêm instance chỉ là thêm một dòng `server` (Demo B) |
| DB có phải điểm chết không (single point of failure)? | Có, SQL Server đang chạy 1 bản. Bản tiếp theo cần replica hoặc dùng DB managed. Nói thật là hạn chế |
| Session lưu ở đâu? | Không lưu trong RAM của instance (stateless). JWT trong cookie, trạng thái chung nằm ở Redis và DB, nên request nào đi instance nào cũng được |
