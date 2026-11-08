# 08. Xét duyệt yêu cầu hoàn trả đơn hàng

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/return-requests` (danh sách), `/return-requests/:id` (chi tiết, có tin nhắn). Policy `ManageOrders` |
| API (`/api/AdminReturnRequests`) | `GET ?status=`, `GET {id}`, `GET {id}/messages`, `POST {id}/approve`, `POST {id}/adjudicate`, `POST {id}/return-label`, `POST {id}/reject`, `POST {id}/freeze` |
| Tự leo thang | `Infrastructure/Services/ReturnRequestEscalationService.cs`: yêu cầu `Pending` quá **3 ngày** mà seller không phản hồi thì chuyển `Escalated` (quét mỗi giờ) và báo realtime |
| Trạng thái | `ReturnStatuses`: Pending, Approved, WaitingForReturnLabel, ReturnLabelProvided, AwaitingShipment, InTransit, Delivered, Completed, Rejected, Escalated, Frozen |

Luồng chính:

```
Pending ─(seller im lặng 3 ngày)→ Escalated ─(admin phân xử)→ Approved → cấp mã vận đơn trả hàng → InTransit → Delivered → Completed (hoàn tiền)
        └→ Rejected (không đủ điều kiện)
        └→ Frozen (đóng băng khi nghi gian lận, chờ điều tra)
```

## Để làm gì

- Người mua được bảo vệ khi hàng lỗi hoặc không đúng mô tả.
- Seller không thể im lặng để "câu giờ": quá 3 ngày, hệ thống tự đẩy lên admin.
- Admin có nút **Freeze** cho ca nghi lừa đảo, ví dụ buyer gửi trả cục gạch.
- Sai bước (ví dụ cấp mã vận đơn cho yêu cầu đã bị từ chối) thì server trả 400 kèm lý do, không đổi dữ liệu.

## Kịch bản demo

1. Vào `/mock`, bấm **Generate Return Request** 2 lần.
2. Đăng nhập Support, vào **Return Requests**, lọc `status=Pending` rồi `Escalated`.
3. Mở một yêu cầu:
   - lý do, ảnh, đơn gốc;
   - khung tin nhắn giữa buyer, seller, admin.
4. Bấm **Adjudicate Request** (chọn *Require Item Return & Refund* hoặc *Refund without requiring return*), rồi cấp **Return label** (mã vận đơn). Trạng thái đổi theo từng bước.
5. Với yêu cầu thứ hai, bấm **Freeze Request** và ghi lý do nghi gian lận. Mọi thao tác khác bị khóa cho đến khi gỡ.
6. Thử bấm **Return label** trên yêu cầu đã Rejected. Server trả 400 và nói rõ trạng thái hiện tại không cho phép.
7. Giải thích leo thang tự động:
   - mở `ReturnRequestEscalationService.cs`, chỉ dòng `AddDays(-3)`;
   - muốn demo trực tiếp thì sửa `CreatedAt` của một yêu cầu Pending lùi 4 ngày bằng SQL, rồi restart app (job quét ngay khi khởi động).

## Dự phòng

```bash
dotnet test tests/Application.FunctionalTests --filter "WrongStatus_Returns400WithReason_AndDoesNotChangeRequest"
```
