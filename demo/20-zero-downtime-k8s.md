# 20. Đảm bảo Zero Downtime khi hệ thống cập nhật: K8s

## Mô tả trong dự án

| Thành phần | Vị trí / cấu hình |
|---|---|
| Manifest | `k8s/`: `namespace.yaml` (`ebay-clone`), `configmap.yaml`, `secret.example.yaml`, `data-protection-secret.example.yaml`, `db-statefulset.yaml`, `redis-deployment.yaml`, `api-deployment.yaml`, `api-service.yaml`, `nginx-*.yaml` |
| Deployment `ebay-api` | `replicas: 2`, `strategy: RollingUpdate` với **`maxSurge: 1`, `maxUnavailable: 0`** |
| Readiness probe | `/health/ready`: kiểm kết nối SQL Server (`CanConnectAsync`). Pod mới chỉ nhận traffic khi probe này trả 200 |
| Liveness probe | `/health/live`: không đụng DB, nên DB sập không làm kubelet giết cả hai pod |
| Tắt pod êm (graceful shutdown) | `preStop` ngủ 15 giây (endpoint controller kịp gỡ pod khỏi Service, request đang chạy vẫn xong) → `SIGTERM` → host dừng trong 25 giây (đóng SignalR, dừng 5 background service) → `terminationGracePeriodSeconds: 45` |
| SignalR | Redis backplane; client có `withAutomaticReconnect()`, nối lại thì tải lại dữ liệu từ API |
| Migration an toàn | Chỉ **expand** (thêm cột nullable, bảng mới, index), không xóa hay đổi tên khi pod cũ còn chạy (`docs/ExpandContractMigrations.md`). Rollback image không cần migration ngược |
| Job chạy một lần | `IRunOnceGuard` (Redis): 2 pod không cùng chạy duyệt user hoặc monitor |
| Script demo | `scripts/demo-rolling-update.sh <tag-v2>`: `set image` → `rollout status` → gọi `/health/ready` 5 lần → `rollout undo` → kiểm image đã về v1 → `rollout history` |
| Tài liệu | `docs/RollingUpdate.md`, `docs/RollingDeployment.md` |

Trình tự một lần rolling update:

```
Trước:   [v1 pod A ✓] [v1 pod B ✓]
Bước 1:  [v1 A ✓] [v1 B ✓] [v2 C …]            ← maxSurge 1: tạo pod mới trước
Bước 2:  [v1 A ✓] [v1 B ✓] [v2 C ✓ ready]      ← /health/ready = 200 mới nhận traffic
Bước 3:  [v1 A ✓] [v1 B ⏳ preStop 15s] [v2 C ✓] ← maxUnavailable 0: luôn còn ≥ 2 pod phục vụ
Bước 4:  [v1 A ✓] [v2 C ✓] [v2 D …] → lặp lại cho A
Sau:     [v2 C ✓] [v2 D ✓]
```

Nếu v2 lỗi (migration hỏng, không kết nối được DB): pod v2 không bao giờ ready, rollout dừng, **pod v1 vẫn phục vụ**.

## Để làm gì

- Cập nhật phiên bản giữa giờ làm việc mà người dùng không thấy trang lỗi, không bị đăng xuất, không mất request đang chạy.
- Bản mới lỗi thì không chết cả hệ thống, và `rollout undo` về bản cũ trong vài chục giây.

## Kịch bản demo (Docker Desktop Kubernetes)

> **Nói rõ trước lớp:** manifest **chưa apply lên cluster thật**. Demo chạy trên Kubernetes của Docker Desktop (Settings → Kubernetes → Enable).

### Chuẩn bị (làm trước buổi demo, khoảng 15 phút)

```bash
kubectl config use-context docker-desktop
kubectl apply -f k8s/namespace.yaml
cp k8s/secret.example.yaml k8s/secret.yaml                       # điền SA_PASSWORD_*, JWT_KEY, EMAIL_PASSWORD (không commit)
cp k8s/data-protection-secret.example.yaml k8s/data-protection-secret.yaml   # điền ENCRYPTION_KEY (giống .env); file đã nằm trong .gitignore
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml -f k8s/data-protection-secret.yaml
kubectl apply -f k8s/db-statefulset.yaml -f k8s/redis-deployment.yaml
kubectl apply -f k8s/api-deployment.yaml -f k8s/api-service.yaml
kubectl apply -f k8s/nginx-config.yaml -f k8s/nginx-deployment.yaml -f k8s/nginx-service.yaml
kubectl -n ebay-clone get pods        # chờ tất cả Running, READY 1/1
```

- Nginx Service là `LoadBalancer`; trên Docker Desktop nó map ra `http://localhost`.
- Lấy 2 tag image khác nhau trên GHCR (file 17), ví dụ `sha-3b6fd09` (v1) và `sha-e2dda04` (v2).
- Nếu package GHCR đang private: tạo `imagePullSecret`, hoặc chuyển package sang public.

### Demo 1: hiện trạng (30 giây)

```bash
kubectl -n ebay-clone get deploy,pods,svc -o wide
kubectl -n ebay-clone get deploy ebay-api -o jsonpath='{.spec.strategy}{"\n"}'
# {"rollingUpdate":{"maxSurge":1,"maxUnavailable":0},"type":"RollingUpdate"}
```

### Demo 2: cập nhật trong khi có traffic liên tục (2,5 phút)

**Terminal 1**: bắn request liên tục và đếm lỗi:

```bash
ok=0; fail=0; while true; do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost/health/ready)
  [ "$code" = "200" ] && ok=$((ok+1)) || { fail=$((fail+1)); echo "LỖI $code lúc $(date +%T)"; }
  printf "\rOK=%d  FAIL=%d " $ok $fail; sleep 0.2
done
```

**Terminal 2**: xem pod thay nhau:

```bash
kubectl -n ebay-clone get pods -w
```

**Terminal 3**: chạy script demo:

```bash
bash scripts/demo-rolling-update.sh sha-e2dda04
```

Thuyết minh theo Terminal 2:
1. Pod mới xuất hiện (`ContainerCreating` → `Running 0/1` → `1/1`). Chỉ khi `1/1` (đã ready) thì một pod cũ mới chuyển `Terminating`.
2. Pod cũ ở trạng thái `Terminating` khoảng 15 giây (preStop) trước khi biến mất.
3. Lặp lại cho pod thứ hai.

Kết quả ở Terminal 1: **`FAIL=0`** suốt quá trình.

Script tiếp tục:
- in `X-Backend-Server` để thấy request đã vào pod mới;
- `rollout undo` về v1 (rollback cũng là rolling, cũng không lỗi);
- in `rollout history`.

### Demo 3: bản lỗi không làm sập hệ thống (1,5 phút, tùy chọn)

```bash
kubectl -n ebay-clone set image deployment/ebay-api ebay-api=ghcr.io/monomee/ebay_clone_adminrole:tag-khong-ton-tai
kubectl -n ebay-clone get pods     # pod mới ErrImagePull / ImagePullBackOff, 2 pod cũ vẫn Running 1/1
# Terminal 1 vẫn OK, FAIL không tăng
kubectl -n ebay-clone rollout undo deployment/ebay-api
```

Ý nghĩa: `maxUnavailable: 0` cộng readiness probe làm cho bản hỏng **không bao giờ thay được** bản đang chạy tốt.

### Demo 4: giải thích migration không phá pod cũ (30 giây)

Mở `docs/ExpandContractMigrations.md` và migration `20261108095927_IpBlocklist`. Migration chỉ **thêm bảng mới**, nên pod v1 (chưa biết bảng này) vẫn chạy bình thường khi pod v2 đã migrate xong.

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| User đang dùng có bị đăng xuất không? | Không. JWT trong cookie, khóa ký và Data Protection key dùng chung qua secret; token bị thu hồi lưu ở Redis. Pod nào cũng xác thực được |
| WebSocket SignalR đang mở thì sao? | Bị ngắt khi pod cũ dừng. Client tự nối lại vào pod mới, rồi gọi lại API để lấy dữ liệu đã lỡ (DB là nguồn đúng) |
| HPA (tự scale)? | Đã lên kế hoạch (task W8-16), chưa có manifest HPA |

## Dự phòng

Nếu không chạy được k8s trên máy demo:
- chạy rolling bằng Docker Compose: `docker stop ebay_api_1` trong khi vòng `curl` vẫn chạy (file 15, demo 3);
- giải thích cấu hình qua `k8s/api-deployment.yaml` và `docs/RollingUpdate.md`.
