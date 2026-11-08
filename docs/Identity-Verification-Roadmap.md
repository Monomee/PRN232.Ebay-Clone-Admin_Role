# Xác minh danh tính: hiện trạng và hướng phát triển (11/2026)

Tài liệu cho báo cáo: ba lớp xác minh **chưa tích hợp** (eKYC CCCD, OTP SMS, xác minh tài khoản ngân hàng qua Napas): hệ thống hiện làm được gì, vì sao chưa tích hợp, và tích hợp thế nào.
GeoIP đã làm (DB-IP Lite), xem `Security-Hardening-2026-11.md` mục 1.15.

## 0. Tóm tắt

| Lớp | Hiện trạng | Chặn được | Chưa chặn được | Lý do chưa tích hợp |
|---|---|---|---|---|
| eKYC CCCD | Kiểm **định dạng** CCCD (mã tỉnh, giới tính/thế kỷ, năm sinh); 1 CCCD = 1 tài khoản qua điểm rủi ro | Số CCCD bịa; dùng lại CCCD của tài khoản khác | Dùng CCCD **thật của người khác** (lộ lọt dữ liệu) | Cần tài khoản/hợp đồng với nhà cung cấp eKYC, xử lý ảnh giấy tờ + khuôn mặt (dữ liệu nhạy cảm) |
| OTP SMS | SĐT do người dùng tự khai, chỉ dùng để liên kết tài khoản (+30 điểm nếu trùng) | SĐT trùng giữa các tài khoản | SĐT không có thật / không thuộc người khai | Gửi SMS OTP ở Việt Nam cần brandname đăng ký với nhà mạng (doanh nghiệp); bản dùng thử quốc tế không ổn định |
| Ngân hàng (Napas) | Tên chủ tài khoản phải khớp tên KYC tự khai; 1 tài khoản ngân hàng = 1 seller; đổi tài khoản thì giữ tiền 48h + email cảnh báo | Rút về tài khoản tên người khác (nếu tên KYC thật); nhiều shop chung 1 tài khoản | Tên KYC tự khai sai từ đầu; số tài khoản không tồn tại | Tra tên chủ tài khoản cần hợp đồng doanh nghiệp với Napas hoặc cổng thanh toán |

Ba lớp này bổ sung cho nhau: eKYC chứng minh **người thật**, SMS chứng minh **SĐT thuộc người đó**, Napas chứng minh **tiền về đúng người đó**. Khi có cả ba, tạo tài khoản clone phải có CCCD + SIM + tài khoản ngân hàng thật của người khác cho mỗi tài khoản.

## 1. eKYC CCCD

### Nhà cung cấp tham khảo

- **FPT.AI eKYC** (console.fpt.ai): OCR CCCD, so khớp khuôn mặt (face matching), kiểm tra người thật (liveness). Có quota dùng thử, đăng ký tự phục vụ.
- **VNPT eKYC**, **Viettel AI eKYC**: tương tự, thường qua hợp đồng doanh nghiệp.
- **Đọc chip NFC** của CCCD gắn chip (chống giả mạo tốt nhất) cần ứng dụng di động, không làm được trên web.

### Luồng đề xuất

1. Seller (bắt buộc) / buyer (tùy chọn) upload ảnh **mặt trước, mặt sau** CCCD và **ảnh selfie**.
2. Server gọi OCR → nhận số CCCD, họ tên, ngày sinh. **So với thông tin đã khai khi đăng ký** (`VerificationDocuments`); lệch thì từ chối.
3. Gọi face matching (selfie ↔ ảnh trên CCCD) và liveness; điểm dưới ngưỡng thì chuyển admin duyệt tay.
4. Đạt: `User.IsVerified = true`, ghi `SensitiveDataLog` (`KycVerified`), **xóa ảnh** sau khi xác minh (chỉ giữ kết quả + mã giao dịch của nhà cung cấp).
5. Bắt buộc `IsVerified` với seller trước khi đăng bán và nhận payout (thêm một điều kiện vào `AccountStanding`).

### Thay đổi trong code

- Cột mới trên `User`: `KycProvider`, `KycReference`, `KycVerifiedAt` (migration chỉ thêm, an toàn rolling update).
- `IKycVerifier` (Application) + `FptAiKycVerifier` (Infrastructure), giống cách `ICaptchaVerifier`/`TurnstileCaptchaVerifier` đang làm; cấu hình `Kyc:ApiKey`.
- Endpoint `POST /api/Account/kyc` (multipart), giới hạn kích thước ảnh, rate limit strict.
- Trang `/seller-kyc` (React) và hàng chờ duyệt tay cho admin (giống tab Risk Review).

### Rủi ro / lưu ý

- Ảnh CCCD và khuôn mặt là **dữ liệu cá nhân nhạy cảm** (Nghị định 13/2023/NĐ-CP): cần sự đồng ý rõ ràng, mã hóa khi lưu tạm, xóa sau khi xác minh, ghi log truy cập.
- Chi phí theo lượt gọi API; chỉ bắt buộc với seller để giới hạn chi phí.

**Ước tính:** đăng ký nhà cung cấp vài giờ – 1, 2 ngày; code 2–3 ngày (kể cả giao diện và test).

## 2. OTP SMS

### Nhà cung cấp tham khảo

- **Trong nước:** eSMS.vn, SpeedSMS, VietGuys: gửi OTP qua **brandname** đã đăng ký với nhà mạng (cần giấy tờ doanh nghiệp, mất 1–3 tuần).
- **Quốc tế:** Twilio Verify, Vonage: tài khoản dùng thử chỉ gửi tới số đã xác minh; tỉ lệ tới máy ở Việt Nam không ổn định nếu không có brandname.
- (Đã thử đăng ký Twilio ngày 08/11/2026: trang đăng ký báo lỗi "Something went wrong", chưa tạo được tài khoản.)

### Luồng đề xuất

Dùng lại nguyên cơ chế `EmailOtp` (`Application/Authentication/Commands/Register/EmailVerification.cs`):

- Mã 6 số, DB chỉ lưu HMAC, hết hạn 10 phút, sai 5 lần phải xin mã mới, gửi lại tối đa 1 lần/phút.
- Thêm cột `PhoneVerifiedAt`, `PhoneOtpHash`, `PhoneOtpSentAt`, `PhoneOtpAttempts`.
- `ISmsSender` (Application) + `EsmsSender`/`TwilioSender` (Infrastructure) + chế độ `LogInsteadOfSending` cho dev như email.
- Chỉ SĐT **đã xác minh** mới tạo định danh `Phone` trong `UserIdentifiers` và mới được tăng trọng số liên kết (vì đã chứng minh sở hữu SIM).
- Chống lạm dụng gửi SMS (SMS pumping, tốn tiền): rate limit theo IP và theo SĐT, captcha trước khi gửi, chặn đầu số quốc tế.

**Ước tính:** brandname 1–3 tuần; code 0,5–1 ngày.

## 3. Xác minh tài khoản ngân hàng (Napas)

### Cách làm trên thực tế

- **Tra tên chủ tài khoản** (account name inquiry) qua Napas hoặc cổng trung gian (cổng thanh toán, ngân hàng đối tác): nhập số tài khoản + mã ngân hàng → trả về tên chủ tài khoản. Cần hợp đồng doanh nghiệp.
- **Chuyển thử (penny drop):** chuyển một khoản nhỏ kèm mã ngẫu nhiên trong nội dung chuyển khoản; seller nhập lại mã để chứng minh sở hữu tài khoản.

### Luồng đề xuất

1. `PUT /api/Account/bank-account` gọi `IBankAccountVerifier.LookupHolderNameAsync(bankCode, accountNumber)`.
2. So tên trả về với tên **đã eKYC** (mục 1) bằng `AccountLinking.NamesMatch` (đã có, so không dấu). Lệch thì từ chối.
3. Lưu kết quả (`BankVerifiedAt`, mã giao dịch tra cứu). Payout chỉ chạy tới tài khoản đã xác minh.
4. Giữ nguyên các luật hiện có: 1 tài khoản ngân hàng = 1 seller, giữ tiền 48h khi đổi, email cảnh báo.

### Trong phạm vi đồ án

Không ký được hợp đồng Napas, nên nếu cần demo thì làm **bản mô phỏng** `MockBankAccountVerifier` (giống `MockPaymentGatewayService` đang có): trả tên chủ tài khoản từ một bảng dữ liệu mẫu, gắn nhãn rõ là mock; khi có hợp đồng chỉ thay phần Infrastructure.

**Ước tính:** hợp đồng vài tuần – vài tháng; code thật 0,5–1 ngày; bản mô phỏng 2–3 giờ.

## 4. Thứ tự đề xuất khi phát triển tiếp

1. **eKYC cho seller**: chặn trực tiếp hình thức nguy hiểm nhất (seller bị ban quay lại bằng CCCD người khác).
2. **Napas**: sau eKYC, vì cần tên **đã xác minh** để so.
3. **OTP SMS**: tăng độ tin cậy cho liên kết qua SĐT; ưu tiên thấp hơn vì đã có OTP email + captcha.
