# 03. Danh sách sản phẩm: xoá hoặc ẩn các sản phẩm vi phạm

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/products` (`ProductModerationPage.jsx`), policy `ManageProducts` (Support, SuperAdmin) |
| API | `GET /api/Products/managed` (lọc, phân trang), `GET managed/summary`, `GET managed/{id}/violation-details`, `POST managed/{id}/resolve-violation` |
| Xử lý | `Application/Products/Commands/ResolveProductViolation/ResolveProductViolation.cs` |
| Nguồn vi phạm | Người dùng report: `POST /api/products/reports` (bắt buộc đăng nhập; VeRO, tức vi phạm sở hữu trí tuệ, được xếp Critical) |

4 hành động xử lý vi phạm:

| Hành động | Kết quả |
|---|---|
| **Delete & Warn** | Xóa sản phẩm và cộng lỗi vi phạm cho seller, gửi cảnh báo |
| **Hide** | Ẩn khỏi trang công khai (sửa được rồi mở lại), báo cho seller |
| **Reject** | Bác report: sản phẩm không vi phạm |
| **Restore** | Khôi phục sản phẩm đã ẩn |

## Để làm gì

- Sàn phải gỡ hàng giả, hàng cấm, ảnh vi phạm bản quyền thật nhanh.
- Các mức xử lý khác nhau, để không phải lỗi nhỏ nào cũng xóa.
- Report được gom theo sản phẩm. Mỗi người chỉ có 1 report đang chờ cho mỗi sản phẩm, nên spam report không đẩy được sản phẩm lên đầu.

## Kịch bản demo

1. Đăng nhập Support (`support@ebay.local`), vào **Products**.
2. Chỉ vào các ô tổng (summary) và bộ lọc theo trạng thái hoặc độ ưu tiên.
3. Mở một sản phẩm bị report. Mục **Violation details** cho thấy:
   - ai report,
   - lý do,
   - số lượt report.
4. Bấm **Hide**:
   - mở `/product/<id>` (trang công khai) ở tab khác: sản phẩm không còn hiện;
   - bấm **Restore** thì hiện lại.
5. Chọn sản phẩm khác, bấm **Delete & Warn**:
   - sản phẩm biến mất;
   - mở `/users/<sellerId>`: thấy seller bị cộng vi phạm, kèm thông báo.
6. Chọn sản phẩm thứ ba, bấm **Reject** (report sai). Report đóng, sản phẩm giữ nguyên.

Nếu chưa có sản phẩm bị report: đăng nhập `john@example.com` (buyer), mở một sản phẩm và report.

## Câu hỏi hay gặp

- **Seller tự xóa được sản phẩm không?** Được, qua trang `/seller-products`. Còn trang này là của admin để xử lý vi phạm.
- **Có xóa cứng không?** Hide và Restore là ẩn mềm. Delete & Warn xử lý theo luồng vi phạm và có ghi lại lịch sử.
