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

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| DB có phải điểm chết không (single point of failure)? | Có, SQL Server đang chạy 1 bản. Bản tiếp theo cần replica hoặc dùng DB managed. Nói thật là hạn chế |
| Session lưu ở đâu? | Không lưu trong RAM của instance (stateless). JWT trong cookie, trạng thái chung nằm ở Redis và DB, nên request nào đi instance nào cũng được |
