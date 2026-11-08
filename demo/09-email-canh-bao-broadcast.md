# 09. Gửi email cảnh báo hệ thống hoặc thông báo toàn bộ người dùng

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/broadcasts` (`BroadcastPage.jsx`), policy `ManageBroadcasts` (Support, SuperAdmin) |
| API admin | `GET /api/Broadcasts`, `POST /api/Broadcasts` (gửi ngay), `POST /api/Broadcasts/schedule` (hẹn giờ) |
| API người dùng | `GET` thông báo đang hiệu lực, `POST {id}/read` (đánh dấu đã đọc) |
| Hẹn giờ | `ScheduledBroadcastService` (job nền) gửi khi tới `ScheduleAt` |
| Gửi mail | `IEmailService` qua SMTP Gmail (`EmailSettings`) |
| Email cảnh báo bảo mật tự động | `SecurityAlert` |

Các trường của `SendBroadcastCommand`:
- `TargetAudience`: `All`, `Seller`, `Buyer` hoặc `Group`. `Group` thì bắt buộc `TargetGroup`.
- `Channels`: `Email`, `InApp`, hoặc cả hai.
- `ScheduleAt`: thời điểm gửi nếu hẹn giờ.

`SecurityAlert` không cần admin bấm. Nó tự gửi mail cho chủ tài khoản khi:
- đổi tài khoản ngân hàng;
- đăng nhập ở 2 nơi xa nhau bất thường.

## Để làm gì

- **Thông báo toàn sàn:** bảo trì, đổi chính sách phí, khuyến mãi. Có thể gửi riêng cho seller hoặc buyer.
- **Cảnh báo bảo mật:**
  - Kẻ chiếm tài khoản có thể xóa thông báo trong app, nhưng không vào được hộp thư của chủ thật.
  - Vì vậy cảnh báo quan trọng phải đi qua email.

## Kịch bản demo

> Demo mail thật: đặt `EmailSettings:LogInsteadOfSending=false` và dùng email nhận mà mình mở được trên máy chiếu.

1. Đăng nhập Support, vào **Broadcasts**.
2. Tạo thông báo:
   - Title: *"Bảo trì hệ thống 23:00"*
   - Audience: **Seller**
   - Channels: **InApp + Email**
   Bấm Gửi.
3. Đăng nhập seller `fashion@example.com` ở cửa sổ khác.
   - Thấy thông báo in-app ngay (realtime).
   - Bấm đánh dấu đã đọc.
4. Đăng nhập buyer `john@example.com`: **không** thấy thông báo, vì audience chỉ là Seller.
5. Mở Gmail của tài khoản seller (nếu email thật): có mail.
6. **Hẹn giờ:** tạo broadcast với `ScheduleAt` = 2 phút sau. Danh sách hiện trạng thái chờ gửi. Đến giờ thì tự gửi.
7. **Email cảnh báo bảo mật:**
   - seller vào `/seller-bank-account`, đổi tài khoản ngân hàng;
   - chủ tài khoản nhận mail "tài khoản ngân hàng đã thay đổi";
   - payout bị giữ 48 giờ (xem file 21).
8. Nếu để `LogInsteadOfSending=true`: chỉ vào console, thấy dòng `LogInsteadOfSending - mail to ...`. Máy dev không bao giờ gửi nhầm mail thật.

## Câu hỏi hay gặp

- **Test tự động có gửi mail thật không?** Không. Test thay `IEmailService` bằng bản giả.
- **Gửi lỗi thì sao?** Thay đổi nghiệp vụ đã được lưu trước. Gửi mail lỗi chỉ ghi log, không hoàn tác.
