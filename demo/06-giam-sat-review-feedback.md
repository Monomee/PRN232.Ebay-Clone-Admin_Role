# 06. Giám sát hệ thống đánh giá – phản hồi (Review + Feedback)

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/review-monitoring` (`ReviewMonitoringPage.jsx`), policy `ModerateReviews` |
| API admin | `GET /api/Reviews/flagged`, `GET statistics`, `GET seller-feedbacks`, `PUT {id}/status` (`Visible` / `Hidden`) |
| API người dùng | `POST /api/Reviews` (viết review), `POST {id}/reply` (seller trả lời), `POST {id}/report` (báo cáo review) |
| Tự gắn cờ | `Application/Reviews/Commands/CreateReviewCommand.cs` |

Review bị gắn cờ tự động trong 3 trường hợp:
1. **LowRating:** đánh giá thấp. Đưa vào để admin xem có phải tấn công seller không.
2. **LinkedToSeller (review chéo / shill review):** người review có chung CCCD, ngân hàng, email, SĐT, thiết bị hoặc địa chỉ với seller.
   - Review bị giữ ở `PendingReview`, không hiện công khai.
   - Nếu chỉ trùng IP hoặc tọa độ thì không tính (wifi chung).
3. **AI moderation** (`OpenAiModerationService`, chạy nền): phát hiện nội dung độc hại hoặc xúc phạm. Cần `OpenAI:ApiKey`; không có key thì bỏ qua bước này.

Ngoài ra, tài khoản đang Pending, Suspended hoặc Banned **không viết review được** (`AccountStanding`).

## Để làm gì

- Review là nền tảng niềm tin của sàn.
- Phải chặn được 2 kiểu gian lận:
  - seller tự tạo tài khoản phụ để khen mình (shill);
  - đối thủ chê bôi.
- Phải chặn được nội dung bẩn.
- Admin chỉ cần xem danh sách đã lọc sẵn, không phải đọc hết mọi review.

## Kịch bản demo

1. Đăng nhập Support, vào **Review Monitoring**.
2. Chỉ vào **statistics**: phân bố số sao, số review bị gắn cờ.
3. Tab **Flagged**: mỗi dòng ghi lý do (`LowRating`, `LinkedToSeller`, hoặc lý do do AI đưa ra).
4. Chọn một review xúc phạm, bấm **Hide**: review biến mất khỏi trang sản phẩm. Bấm lại để chuyển về **Visible**.
5. Tab **Seller feedbacks**: phản hồi theo seller. Dùng để thấy seller nào đang bị đánh giá xấu nhiều.
6. *(Ăn điểm bảo mật)* Demo shill review:
   - dùng 2 tài khoản có chung số điện thoại (seller và một buyer đăng ký cùng SĐT);
   - buyer viết review 5 sao cho sản phẩm của seller đó;
   - review vào Flagged với lý do `LinkedToSeller`, không hiện công khai.
   - Muốn nhanh hơn: chạy test (bên dưới).

## Dự phòng

```bash
dotnet test tests/Application.UnitTests --filter "FullyQualifiedName~Review"
```
