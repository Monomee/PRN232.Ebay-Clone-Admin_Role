# 07. Quản lý danh sách khiếu nại (Dispute): duyệt, điều phối, phân xử

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/disputes` (docket), `/disputes/dashboard` (thống kê), `/disputes/:id` (chi tiết). Policy `ManageDisputes` |
| API | `GET /api/Disputes` (lọc/phân trang), `GET statistics`, `GET {id}`, `POST {id}/assign`, `POST {id}/resolve` |
| Phân xử | `ResolveDisputeCommand` |
| Realtime | SignalR hub `/hubs/dispute` (cần `ManageDisputes`): admin khác thấy dispute được nhận hoặc đóng ngay, không cần refresh |
| Tài liệu | `docs/DISPUTE-SYSTEM-ANALYSIS.md`, `docs/DISPUTE-DATABASE-DIAGRAM.md` |

Các trường của `ResolveDisputeCommand`:
- `Winner`: `Buyer`, `Seller` hoặc `Split`.
- `RefundAmount`: không được âm.
- `AdminNotes`.
- `RequireReturn`: buộc gửi trả hàng.
- `AddSellerViolation`: cộng lỗi cho seller.
- `SendNotifications`.

Luồng:

```
Open --(assign: điều phối cho 1 admin)--> UnderReview --(resolve: Buyer/Seller/Split + số tiền)--> Resolved
```

## Để làm gì

- Khi người mua và người bán tranh chấp, sàn là trọng tài.
- **Điều phối:** gán dispute cho từng admin, tránh 2 người cùng xử lý một ca.
- **Phân xử:** chọn bên thắng, hoàn tiền một phần hoặc toàn bộ, phạt seller nếu cần. Hai bên đều nhận thông báo.

## Kịch bản demo

1. Chuẩn bị: vào `/mock`, bấm **Generate Open Dispute** 2–3 lần.
2. Đăng nhập Support, vào **Disputes**:
   - lọc theo trạng thái;
   - chỉ vào cột hạn xử lý và độ ưu tiên.
3. Mở `/disputes/dashboard`: số dispute mở, đã xử lý, tỉ lệ thắng của buyer/seller.
4. **Điều phối (realtime):**
   - mở cùng dispute ở 2 cửa sổ: SuperAdmin và Support;
   - Support bấm **Assign to me**;
   - cửa sổ SuperAdmin cập nhật người phụ trách ngay, không cần F5 (SignalR).
5. Đọc chi tiết: tin nhắn giữa hai bên, bằng chứng, thông tin đơn.
6. **Phân xử:**
   - chọn **Split**, hoàn 50% số tiền, tick *Add seller violation*, ghi chú *"Hàng giao thiếu phụ kiện"*;
   - bấm **Resolve**: trạng thái chuyển Resolved;
   - mở `/users/<sellerId>`: thấy vi phạm vừa cộng.
7. Thử nhập số tiền âm, bấm Resolve: server từ chối (*Refund amount cannot be negative*). Kiểm tra dữ liệu ở server, không chỉ ở form.

## Câu hỏi hay gặp

- **Seller có nghe lén được kênh realtime không?** Không. Hub `/hubs/dispute` yêu cầu quyền `ManageDisputes`; seller kết nối sẽ bị 403. Test `AdminHubs_RefuseSellers`.
- **Admin gán dispute cho người khác có bị giả mạo người gán không?** Người thao tác lấy từ JWT, không lấy từ body request.
