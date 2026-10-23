# Rolling update trên eBay Clone

Tài liệu này mô tả cách Deployment `ebay-api` thay pod mà không ngắt dịch vụ, chuyện gì xảy ra với SQL Server trong lúc đó, và script `scripts/demo-rolling-update.sh` làm những bước nào.

Chiến lược đổi schema cho an toàn với pod cũ nằm ở [ExpandContractMigrations.md](ExpandContractMigrations.md).

## Trên dự án

Cluster chạy namespace `ebay-clone`. Người dùng đi qua Nginx (`ebay-nginx-service`, cổng 80) tới Service `ebay-api-service`, rồi tới các pod API cổng 8080. SignalR dùng Redis để pod cũ và pod mới cùng nhận message.

Deployment `ebay-api` để 2 replica và chiến lược `RollingUpdate` với `maxSurge: 1`, `maxUnavailable: 0`. Kubernetes tạo một pod mới trước, và chỉ xóa một pod cũ sau khi pod mới đã sẵn sàng. Số pod nhận traffic không xuống 0.

Một pod mới được nhận traffic khi `/health/ready` trả 200. Endpoint này gọi `CanConnectAsync()` tới SQL Server. Process chết cứng thì `/health/live` thất bại và kubelet mới restart container. Liveness không mở database, nên SQL Server sập không làm cả hai pod bị giết.

Khi một pod cũ bị xóa:

1. `preStop` ngủ 15 giây. Process vẫn phục vụ request đang mở. Endpoint controller gỡ pod khỏi Service, request mới sang pod còn lại.
2. Hết 15 giây, kubelet gửi `SIGTERM`. ASP.NET đóng hub SignalR. Năm background service thoát vòng lặp. Host tự dừng trong 25 giây.
3. `terminationGracePeriodSeconds` là 45. Hết giờ thì `SIGKILL`.

Client SignalR đã bật `withAutomaticReconnect()`. Khi nối lại, trang dispute, thông báo, return request và ví seller gọi lại API. Database là nguồn đúng. Event rơi đúng lúc socket đứt được thay bằng lần đọc đó.

Worker trên pod còn sống vẫn quét. Pod mới chạy settlement, auto-review, đánh giá seller và leo thang return request ngay khi process lên. Payout ngủ đến 2:00 UTC. Dòng payout `Processing` cũ hơn 45 giây được làm tiếp ở phiên sau, trên đúng dòng đó.

## Trên database

Pod cũ và pod mới dùng chung một SQL Server. Pod mới gọi `MigrateAsync()` trong `InitialiseDatabaseAsync()` trước khi nghe cổng 8080. Schema đổi trong lúc pod cũ còn ghi.

EF ghi migration đã chạy vào `__EFMigrationsHistory` và khóa bảng đó. Hai pod mới cùng khởi động thì pod đến sau chờ, rồi thấy migration đã xong.

Migration commit xong thì pod cũ thấy schema mới ngay. Vì vậy migration của bản đang deploy chỉ được thêm cột nullable, cột có default, bảng mới hoặc index mới. Xóa cột, đổi tên, đổi kiểu, hoặc `NOT NULL` khi pod cũ vẫn insert mà không gửi giá trị sẽ làm pod cũ lỗi giữa chừng.

Readiness chỉ qua sau khi process đã nghe cổng và `CanConnectAsync()` thành công. Migration ném exception thì process không nghe cổng, probe thất bại, rollout dừng. `maxUnavailable: 0` giữ pod cũ tiếp tục phục vụ.

Rollback Deployment trả image về bản trước. Nó không chạy migration ngược. Cột đã thêm vẫn nằm trong database. Đó là lý do bản mới phải tương thích ngược với bản đang chạy: sau `rollout undo`, pod v1 vẫn đọc và ghi được schema mà v2 vừa áp.

## Chạy script demo

Hai tag phải có sẵn trên `ghcr.io/monomee/ebay_clone_adminrole`. Script không build và không push. `kubectl` phải trỏ đúng cluster, namespace `ebay-clone` đã có Deployment `ebay-api`.

Trong Git Bash, tại thư mục repo:

```bash
bash scripts/demo-rolling-update.sh sha-<v2>
```

Nginx không nằm ở `http://localhost` thì chỉ đường khác:

```bash
BASE_URL=http://localhost:8080 bash scripts/demo-rolling-update.sh sha-<v2>
```

`<v2>` là tag khác image đang chạy, thường là `sha-<short>` do CD đẩy lên. Trùng image đang chạy thì script dừng.

## Script đang làm gì

1. Đọc image của container `ebay-api` và gọi đó là v1.
2. Ghép tag tham số thành `ghcr.io/monomee/ebay_clone_adminrole:<v2>`.
3. `kubectl set image` để Deployment dùng v2. Kubernetes rollout: pod mới lên, `/health/ready` qua, rồi một pod cũ bị xóa. Lặp lại cho replica còn lại. `rollout status` chờ tối đa 300 giây.
4. Gọi `GET {BASE_URL}/health/ready` năm lần và in mã HTTP cùng header `X-Backend-Server`. Header này do Nginx thêm, cho thấy request rơi vào pod nào. Kỳ vọng HTTP 200.
5. `kubectl rollout undo` đưa spec về revision trước, tức image v1 đã ghi ở bước 1. Chờ `rollout status` thêm 300 giây.
6. Đọc lại image. Khác v1 đã ghi thì script thoát với lỗi.
7. In `kubectl rollout history` để thấy revision vừa undo.

Nếu bước 3 hết giờ vì pod v2 không qua readiness, script dừng trước undo. Pod cũ vẫn phục vụ. Lúc đó tự chạy `kubectl -n ebay-clone rollout undo deployment/ebay-api` nếu muốn về hẳn v1.
