# 04. Quản lý đơn hàng toàn hệ thống

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/orders` (`OrdersPage.jsx`), policy `ManageOrders` (Support, SuperAdmin, Administrator) |
| API | `GET /api/Orders?PageNumber=&PageSize=&Status=&Search=`, `GET /api/Orders/summary`, `GET /api/Orders/{id}` |
| Query | `Application/Orders/Queries/GetOrdersWithPagination`: phân trang ở DB, lọc theo trạng thái, tìm theo mã đơn hoặc tên người mua |
| Liên quan | Tiền của đơn đi qua settlement và payout (`/payout-engine`, `/sellers/pending/:id`) |

## Để làm gì

- Admin tra cứu bất kỳ đơn nào trên toàn sàn khi có khiếu nại hoặc hoàn trả: đơn của ai, bao nhiêu tiền, trạng thái gì.
- Có phân trang ở server, nên vẫn chạy nhanh khi có hàng chục nghìn đơn. JMeter plan 03 đã đo (xem file 19).

## Kịch bản demo

1. Đăng nhập Support, vào **Orders**.
2. Chỉ vào các ô summary: số đơn theo trạng thái.
3. Lọc theo trạng thái, ví dụ `Delivered`, rồi gõ tên người mua vào ô tìm kiếm.
4. Chuyển trang 1 → 2 → 3. Mỗi lần chuyển chỉ tải một trang dữ liệu: mở DevTools > Network, thấy `PageNumber=2`.
5. Mở chi tiết một đơn: sản phẩm, người mua, người bán, tiền, trạng thái.
6. Liên kết sang luồng khác: lấy mã đơn này tạo return request hoặc dispute ở `/mock`. Phần này nối sang file 07 và 08.
7. *(Tùy chọn)* Đăng nhập Monitor rồi mở `/orders`: bị chặn. Monitor không có quyền `ManageOrders` (xem file 11).

## Dự phòng

`GET /api/Orders?PageNumber=1&PageSize=10` trong Swagger (`/api`).
