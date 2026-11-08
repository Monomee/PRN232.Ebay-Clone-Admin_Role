# 01. Dashboard tổng quan: số lượng user, sản phẩm, đơn hàng

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/dashboard` (cũng là trang `/`). File `ClientApp/src/pages/DashboardPage.jsx` |
| Component | `components/dashboard/`: `MetricTiles`, `RevenueChart`, `OrderDistribution`, `ActionCenter`, `DashboardActions`, `PlatformFeeWidget`, `SellerLevelConfigWidget` |
| API | `GET /api/Dashboard/metrics` (policy `ViewDashboard`) và `GET /api/Stats/dashboard` |
| Query | `Application/Dashboard/Queries/GetDashboardMetrics`. DTO gồm `TotalUsers`, `TotalProducts`, `TotalOrdersToday`, `PendingAccountsCount`, `OpenDisputesCount`, `NewReturnRequestsCount`, `WeeklyRevenue` |

Ai xem được:
- `SuperAdmin`, `Support`, `Monitor`, và custom role có quyền `ViewDashboard`.
- Biểu đồ doanh thu và đơn hàng chỉ hiện khi có thêm quyền `ViewReports` (Monitor, SuperAdmin).
- Widget phí sàn và tiêu chí cấp seller chỉ hiện cho vai trò được phép sửa.

## Để làm gì

- Admin vào là thấy ngay "sức khỏe" của sàn: bao nhiêu user, sản phẩm, đơn hôm nay, doanh thu tuần.
- **Action Center** gom việc đang chờ xử lý: tài khoản chờ duyệt, dispute đang mở, yêu cầu hoàn trả mới. Admin biết nên làm gì trước.

## Kịch bản demo

1. Đăng nhập `superadmin@ebay.local` (mật khẩu, rồi mã 2FA). App tự vào `/dashboard`.
2. Chỉ vào hàng **Metric Tiles**: tổng user, tổng sản phẩm, đơn hôm nay.
3. Chỉ vào **Action Center**: số tài khoản chờ duyệt, dispute mở, return request mới.
   - Bấm vào một mục để chuyển sang trang xử lý tương ứng.
4. Chứng minh số liệu là thật:
   - Mở tab khác, vào `/mock`, bấm **Generate Purchase** (tạo đơn giả).
   - Quay lại dashboard, refresh. Số đơn hôm nay tăng 1.
5. So sánh theo vai trò: đăng nhập `monitor@ebay.local` ở cửa sổ khác.
   - Monitor thấy dashboard và biểu đồ.
   - Monitor không thấy widget sửa phí sàn.
   - Sidebar của Monitor không có Users, Products...
   - Phần này nối sang file 11.

## Câu hỏi hay gặp

- **Số liệu lấy realtime hay cache?** Mỗi lần mở trang thì query trực tiếp DB, không cache.
- **Admin không chuyên có hiểu không?** Mỗi ô có icon, nhãn rõ, và bấm vào được để sang trang chi tiết (xem file 14).

## Dự phòng

Gọi API thô trong DevTools Console: `await (await fetch('/api/Dashboard/metrics')).json()`.
