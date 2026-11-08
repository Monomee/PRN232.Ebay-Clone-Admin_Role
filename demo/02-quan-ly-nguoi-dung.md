# 02. Danh sách người dùng: duyệt tài khoản mới, khoá/mở khoá

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Trang | `/users` (`UsersPage.jsx`), chi tiết `/users/:userId` (`UserDetailPage.jsx`), seller `/sellers` |
| API (`/api/Users`, policy `ManageUsers`) | `GET ""` (phân trang, lọc), `GET summary`, `GET {id}`, `POST {id}/approve`, `POST {id}/reject`, `POST {id}/ban`, `POST {id}/unban`, `PUT {id}/status` (Suspend/Activate), `POST {id}/lift-restrictions` |
| Duyệt tự động | `Infrastructure/Services/AutoReviewUsersService.cs`, chạy mỗi 15 giây, chấm điểm rủi ro cho tài khoản mới |
| Kiểm tra quyền thao tác | `Application/Users/Risk/AccountStanding.cs` |

Trạng thái tài khoản:

```
Unverified --(nhập OTP email)--> Pending --(job chấm điểm)--> Approved   (điểm < 30)
                                                          \-> PendingApproval (30–79, admin duyệt tay; tab Risk Review)
                                                          \-> Rejected (≥ 80)
Approved <--> Suspended (tạm ngưng: đăng nhập được, nhưng không đăng bán/review/rút tiền)
Approved <--> Banned (khóa: không đăng nhập được, phiên đang mở bị hủy ngay)
```

Các tab ở trang Users:
- **All**,
- **PendingApproval**: chờ duyệt,
- **RiskReview**: chờ duyệt có điểm rủi ro, điểm cao lên đầu,
- **Banned**.

## Để làm gì

- **Duyệt tài khoản mới:** chặn seller lừa đảo hoặc tài khoản clone trước khi họ đăng bán. Phần lớn tài khoản sạch được duyệt tự động. Admin chỉ xem các ca đáng ngờ.
- **Khóa/mở khóa:** xử lý vi phạm. Ban là nặng nhất. Suspend nhẹ hơn: vẫn đăng nhập xem đơn được, nhưng không bán được. Gỡ hạn chế tự động (Lift restrictions) khi admin xác nhận là khóa nhầm.
- Mọi thao tác ghi **Governance Records** (audit) trên trang chi tiết user.

## Kịch bản demo

### A. Duyệt tài khoản mới (3 phút)

1. Mở cửa sổ ẩn danh, vào `https://localhost:44447/register`, đăng ký **Seller**:
   - Họ tên: `Tran Thi B`
   - CCCD: `001301001234`
   - Ngày sinh: `20/05/2001`
   - Email thật hoặc email bất kỳ
2. Lấy mã OTP:
   - nếu bật `LogInsteadOfSending`: tìm dòng `LogInsteadOfSending - mail to ...` trong console của `dotnet run`;
   - nếu không: mở hộp thư.
   Nhập mã.
3. Thử đăng nhập bằng tài khoản vừa tạo **trước khi** xác minh email. Hệ thống báo: *"Vui lòng xác minh email trước khi đăng nhập"*.
4. Sau khi xác minh: chờ khoảng 15 giây (job chấm điểm), vào `/users` bằng SuperAdmin.
   - Tài khoản sạch thì đã **Approved**.
   - Tài khoản có điểm từ 30 trở lên thì nằm ở tab **Risk Review**. Muốn tạo ca này, xem file 21 mục B (trùng CCCD) hoặc C (đăng ký hàng loạt).
5. Mở chi tiết user trong tab **Risk Review**:
   - card **Account Linking**: điểm, lý do, tài khoản liên quan, *loại* dữ liệu trùng. Không hiện giá trị thật.
   - bấm **Approve** hoặc **Reject**.

### B. Khóa / mở khóa (1–2 phút)

1. Chọn `fashion@example.com`, bấm **Ban**, nhập lý do *"Bán hàng giả"* (có thể đặt `BanUntil`).
2. Ở cửa sổ khác, đăng nhập `fashion@example.com`. Bị từ chối kèm lý do ban.
3. Nếu seller đó **đang đăng nhập sẵn** ở một tab khác: refresh tab đó, phiên bị hủy ngay và chuyển về `/login`.
   - Đây là `SessionValidation` kiểm trạng thái ở mỗi request (xem file 22).
4. Bấm **Unban**. Đăng nhập lại được.
5. **Suspend**: đổi status sang Suspended.
   - Seller vẫn đăng nhập được.
   - Khi đăng sản phẩm thì bị 400 (`AccountStanding`).
   - Activate lại thì hết bị chặn.
6. Kéo xuống **Governance Records**: thấy từng hành động `BanUser`, `UnbanUser`, `UpdateUserStatus`, kèm admin nào làm, lúc nào.

## Câu hỏi hay gặp

- **Sao không bắt admin duyệt hết?** Vì không làm kịp với sàn lớn. Hệ thống tự duyệt ca sạch và chỉ đẩy ca rủi ro cho người xem.
- **Ban có giết phiên đang mở không?** Có. JWT vẫn còn hạn nhưng mỗi request đều kiểm trạng thái user trong DB.

## Dự phòng

```bash
dotnet test tests/Application.FunctionalTests --filter "Register_VerifyEmail_ThenWaitsForRiskReview|ChangedAccount_InvalidatesExistingToken"
```
