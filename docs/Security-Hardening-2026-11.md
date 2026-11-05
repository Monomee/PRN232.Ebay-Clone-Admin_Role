# Bảo mật & chống gian lận tài khoản (11/2026)

Tổng hợp các thay đổi trong PR #17 → #20: thay đổi gì, cần chú ý gì, còn thiếu gì, và cách setup bản mới.

| PR | Nội dung |
|---|---|
| #17 | Xác thực, phiên đăng nhập, 2FA, rate limit, header bảo mật |
| #18 | Liên kết tài khoản, chấm điểm rủi ro, giới hạn seller mới, monitor |
| #19 | Lịch sử đăng nhập, thiết bị, đăng nhập ở hai nơi xa nhau, IP datacenter, đổi ngân hàng |
| #20 | Khóa job khi chạy nhiều replica, unique index, xóa lịch sử đăng nhập cũ |

---

## 1. Thay đổi

### 1.1 Xác thực và phiên (PR #17)

- **JWT** nằm trong cookie `auth_token` (HttpOnly, Secure, SameSite=Strict), sống 60 phút. Body của `login`/`verify-2fa` không còn trả token.
- **2FA gắn với bước mật khẩu:** login cấp cookie `2fa_challenge` (HttpOnly, 5 phút, path `/api/Auth`). `verify-2fa` lấy user từ cookie này, bỏ qua `userId` trong body.
- **Mã TOTP chỉ dùng được một lần** trong cửa sổ 30 giây.
- **API bắt buộc 2FA** (trước đây chỉ React kiểm). Tài khoản chưa bật 2FA chỉ gọi được `/api/Auth/me`, `logout`, `enable-2fa`, `verify-2fa-setup`.
- **Kiểm tra phiên mỗi request** (`SessionValidation`). Token cũ bị từ chối ngay khi:
  - tài khoản bị ban hoặc reject,
  - role thay đổi,
  - quyền của custom admin role bị sửa hoặc gỡ,
  - token không có `jti`.
- **Logout thu hồi token** (lưu `jti` vào Redis, hoặc bộ nhớ nếu không có Redis).
- **`X-Forwarded-For`** chỉ được tin khi request đến từ proxy trong `TrustedProxyNetworks`. Allowlist `InternalIps` hỗ trợ CIDR; đã sửa lỗi `"10.*"` khớp nhầm `100.x`.
- **`POST /api/products`** bắt buộc đăng nhập, seller lấy từ JWT.
- **Rate limit** tính theo IP hoặc theo user (trước là một bộ đếm chung). nginx (cả bản k8s) giới hạn `login` và `verify-2fa`. k8s Service đặt `externalTrafficPolicy: Local` để giữ IP thật.
- **Header bảo mật:** CSP (`script-src 'self'` cho app; Swagger được nới), nosniff, X-Frame-Options; tắt header `Server`.
- **Frontend:** API trả 401 thì chuyển về `/login` (`services/sessionGuard.js`).
- **Swagger** vẫn bật bình thường.

### 1.2 Liên kết tài khoản và điểm rủi ro (PR #18, #19)

- **Bảng `UserIdentifiers`** lưu HMAC của giá trị đã chuẩn hóa, **không lưu giá trị thật**. Các loại định danh:
  - CCCD, Email (bỏ dấu chấm và `+tag` của Gmail), SĐT (`0912…` = `+84 912…`), số tài khoản ngân hàng,
  - địa chỉ (bỏ dấu và các từ như "P.", "Q."), IP public, tọa độ (ô khoảng 110 m),
  - thiết bị (cookie `device_id`).
- **Điểm rủi ro** = trọng số mạnh nhất của mỗi loại định danh. Trùng với tài khoản **Banned/Rejected** thì nặng hơn. `Suspended` được tính như tài khoản thường.

  | Loại | Trùng tài khoản thường | Trùng tài khoản bị khóa |
  |---|---|---|
  | CCCD | 80 | 100 |
  | Bank | 50 | 100 |
  | Email | 40 | 60 |
  | Phone | 30 | 60 |
  | Device | 30 | 60 |
  | Address | 15 | 40 |
  | IP | 15 | 30 |
  | Geo | 15 | 20 |

  Cộng thêm tín hiệu riêng của tài khoản: email dùng một lần +20, IP datacenter/VPS +20, tên chủ tài khoản ngân hàng khác tên KYC +40.
- **Duyệt tài khoản mới** (`AutoReviewUsersService`): dưới 30 thì tự duyệt; 30–79 thì giữ `PendingApproval` cho admin xem; từ 80 thì từ chối. Thông báo gửi user không nói trùng thông tin gì.
- **Trang admin:**
  - Chi tiết user có card **Account Linking** (điểm, lý do, tài khoản liên quan, *loại* dữ liệu trùng) và bảng **Recent Sign-ins**.
  - Danh sách user có badge `RISK`.

### 1.3 Giới hạn tài khoản mới và giữ tiền (PR #18, #20)

- **Áp dụng cho:** tài khoản đã qua bước duyệt rủi ro lúc đăng ký (`RiskCheckedAt` khác null), trong 90 ngày đầu, trừ seller `TopRated`/`AboveStandard`.
- **Giới hạn:** tối đa 10 sản phẩm đang bán, giá ≤ 500. Nếu tài khoản phải qua duyệt tay (điểm ≥ 30) thì 3 sản phẩm, giá ≤ 100. Vượt giới hạn trả 400.
- **Giữ payout** (`User.PayoutHoldUntil`, payout engine bỏ qua seller) trong các trường hợp:
  - admin duyệt tài khoản có điểm ≥ 30: 30 ngày,
  - monitor phát hiện liên kết với tài khoản bị ban: 30 ngày,
  - đăng nhập ở hai nơi xa nhau (seller): 48 giờ,
  - thay tài khoản ngân hàng: 48 giờ.

### 1.4 Giám sát sau khi duyệt (PR #18)

- **`AccountMonitor`** chạy mỗi 10 phút:
  - Tài khoản đang hoạt động trùng thông tin với tài khoản bị ban: cảnh báo, hoặc khóa đăng bán 7 ngày và giữ payout 30 ngày.
  - Số sản phẩm đăng trong 24 giờ ≥ 20 và ≥ 5 lần mức bình thường của chính seller đó: khóa đăng bán 1 ngày.
- **Thang xử lý:** cảnh báo → khóa đăng bán → giữ payout (tự động, gỡ được) → Suspend → Ban (chỉ admin).
- **Admin gỡ hạn chế:** `POST /api/Users/{id}/lift-restrictions` (nút **Lift restrictions**). Sau khi gỡ, monitor chỉ xử lý lại khi có bằng chứng mạnh hơn lần trước.

### 1.5 Đăng nhập và thiết bị (PR #19)

- **Bảng `LoginHistories`** ghi mỗi lần cấp phiên: IP, hash thiết bị, nơi đăng nhập, cờ. Đồng thời cập nhật `LastLoginIp`/`LastLoginAt` (trước đây không có code nào cập nhật hai cột này).
- **Cookie `device_id`:** ngẫu nhiên 128 bit, HttpOnly, 400 ngày. DB chỉ lưu HMAC.
- **Cờ đăng nhập:**
  - `NewDevice`: thiết bị chưa từng dùng.
  - `Datacenter`: IP thuộc dải cloud/VPS.
  - `ImpossibleTravel`: cách lần trước ≥ 500 km với tốc độ > 900 km/h. Kèm thông báo; seller bị giữ payout 48 giờ. **Không chặn đăng nhập.**
- **`PUT /api/Account/bank-account`** (chỉ role Seller):
  - Tên chủ tài khoản phải khớp tên KYC (so sánh không dấu); lệch thì trả 400.
  - Thay tài khoản cũ: giữ payout 48 giờ, gửi thông báo, ghi `SensitiveDataLog` `UpdatePaymentInfo` (chỉ 4 số cuối).

### 1.6 Chạy nhiều replica và dữ liệu (PR #20)

- **Khóa job qua Redis** (`IRunOnceGuard`): mỗi lượt chỉ một pod duyệt user (khóa 14 giây) và chạy monitor (khóa khoảng 9,5 phút).
- **Unique index** `(UserId, Type, ValueHash)` trên `UserIdentifiers`.
- **`LoginHistories`** cũ hơn 180 ngày bị xóa tự động.

### 1.7 Chặn tài khoản chưa duyệt, rút tiền và review chéo

- **`AccountStanding`** (`Users/Risk/AccountStanding.cs`): tài khoản bị ban/reject, `Suspended`, hoặc còn `PendingApproval` (chưa qua duyệt rủi ro) **không được**:
  - đăng sản phẩm (và chỉ role `Seller` mới đăng được; command thiếu `SellerId` trả 401),
  - viết review,
  - tạo yêu cầu rút tiền (admin duyệt rút tiền cũng kiểm lại trạng thái và hold).
- **Rút tiền** (`RequestWithdrawalCommand`) áp cùng luật với payout engine:
  - đang `PayoutHoldUntil` thì không rút được,
  - chỉ rút về **tài khoản ngân hàng đã đăng ký** (`PUT /api/Account/bank-account`, đã so tên KYC). Số tài khoản khác trả 400; tên chủ tài khoản lưu lấy từ tài khoản đã đăng ký.
- **Review chéo (shill review):** người review có chung CCCD, ngân hàng, email, SĐT, thiết bị hoặc địa chỉ với seller thì review bị giữ ở `PendingReview`, `FlagReason = LinkedToSeller` để moderator xem. Chỉ trùng IP/tọa độ thì không tính (wifi chung, NAT nhà mạng).

### 1.8 Đăng ký, captcha, OTP email, kiểm tra CCCD

- **API đăng ký** `POST /api/Auth/register` (chỉ API, chưa có giao diện). Chỉ tạo được `Buyer` hoặc `Seller`.
  - **Captcha** Cloudflare Turnstile (`Captcha:TurnstileSecretKey`). Không có key: Production **từ chối** đăng ký; môi trường khác bỏ qua captcha và ghi log cảnh báo.
  - Rate limit strict (5 lần/phút/IP) cho `register`, `verify-email`, `resend-email-code`.
  - Ghi IP đăng ký (`LastLoginIp`) và cookie `device_id` + `LoginHistories`: nhiều tài khoản đăng ký từ cùng một trình duyệt sẽ bị liên kết qua định danh `Device`.
  - Seller bắt buộc khai họ tên, CCCD, ngày sinh, đủ 18 tuổi. Buyer không bắt buộc, nhưng nếu khai CCCD thì phải khai đủ cả ba.
- **Kiểm tra định dạng CCCD** (`CccdFormat`): 12 số, 3 số đầu là mã tỉnh hợp lệ, số thứ 4 (giới tính + thế kỷ) và 2 số tiếp theo phải khớp năm sinh. Chặn số bịa; **không** chứng minh được thẻ có thật (cần eKYC).
- **OTP email:**
  - Tài khoản mới ở `Status = Unverified`: không đăng nhập được, không đăng bán/review/rút tiền, và **chưa được đưa vào duyệt rủi ro**.
  - Mã 6 số, DB chỉ lưu HMAC, hết hạn sau 10 phút, sai 5 lần thì phải xin mã mới, xin lại tối đa 1 lần/phút (`POST /api/Auth/resend-email-code`).
  - Nhập đúng (`POST /api/Auth/verify-email`) thì chuyển sang `Pending` → `AutoReviewUsersService` chấm điểm như trước.
  - `verify-email` và `resend-email-code` trả cùng một câu cho email không tồn tại, để không lộ email nào đã đăng ký.
- **Dev không có SMTP:** `EmailSettings:LogInsteadOfSending=true` in nội dung mail (có mã OTP) ra log thay vì gửi. App **từ chối khởi động** nếu bật ở Production.

### 1.9 Load test

- Cấu hình `Security:AllowApiWithout2FA` cho phép tài khoản chưa bật 2FA gọi API, chỉ dùng khi chạy JMeter. App **từ chối khởi động** nếu bật ở Production. `docker-compose.loadtest.yml` đã bật sẵn.
- JMeter plan 11 gửi cookie `2fa_challenge` thay cho `userId`.

---

## 2. Chú ý

1. **Sau khi deploy PR #17, mọi người phải đăng nhập lại một lần**, vì token cũ không có `jti`.
2. **Tài khoản nào cũng phải bật 2FA** mới dùng được API. Tài khoản test hoặc JMeter chưa bật 2FA sẽ bị 401, trừ khi bật `ALLOW_API_WITHOUT_2FA=true` (chỉ khi load test).
3. **`InternalIpMiddleware` chặn toàn bộ app với IP ngoài `InternalIps`.** Khi deploy, chỉ admin trong mạng nội bộ đăng nhập được, nên các luật theo IP và vị trí (đăng nhập ở hai nơi xa nhau, datacenter) gần như không kích hoạt. Chúng có tác dụng khi có trang buyer/seller mở cho IP public.
4. **Trùng chỉ IP + tọa độ không còn tự ban**: user được giữ ở trạng thái chờ admin duyệt (để tránh khóa oan người dùng chung wifi).
5. **Ngày giờ trên giao diện** đang hiển thị giờ UTC như giờ địa phương. Lỗi này có từ trước và ảnh hưởng toàn app.
6. **Trong lúc rolling update**, pod chạy code cũ (chưa có khóa) vẫn có thể đồng bộ trùng. Index unique chặn lại; pod cũ chỉ ghi log lỗi, dữ liệu không sai.
7. **Test HTTP tự ký token** phải có user khớp trong DB: dùng `Factory.EnsureSessionUser(id, role, permissions...)`. Muốn truyền cấu hình riêng cho một test thì dùng `new Factory { Settings = { ... } }`.
8. **Rolling update lên bản có đăng ký:** pod cũ chưa biết trạng thái `Unverified` nên có thể duyệt tài khoản chưa xác minh email. Chỉ xảy ra khi đã có người đăng ký trong lúc deploy; muốn chắc chắn thì deploy xong mới mở đăng ký.

---

## 3. Chưa làm / hạn chế

| Mục | Hiện trạng | Hướng làm |
|---|---|---|
| Vị trí IP | Bảng mẫu cấp quốc gia (VN, US, JP) | MaxMind GeoLite2 (cần đăng ký key) |
| Dải IP datacenter | Bảng mẫu vài dải | Job tải định kỳ `ip-ranges.json` (AWS), `cloud.json` (Google), Azure ServiceTags |
| UI đăng ký, buyer/seller | Đã có API đăng ký, chưa có giao diện | Form gọi `register` → `verify-email`, nhúng widget Turnstile |
| UI đổi tài khoản ngân hàng | Chỉ gọi qua Swagger/API | Form gọi `PUT /api/Account/bank-account` |
| Thông báo bảo mật | Chỉ in-app (bảng Notification) | Gửi email khi đổi ngân hàng hoặc đăng nhập từ nơi lạ |
| Xác minh thêm khi đăng nhập lạ | Chỉ gắn cờ | Yêu cầu nhập lại 2FA hoặc xác nhận qua email |
| Báo cho admin | Chỉ badge và banner trong trang user | Hàng chờ "tài khoản rủi ro", widget trên dashboard |
| Governance Records | "Lift restrictions" không hiện (bộ lọc chỉ lấy Ban/Reject/Suspend) | Mở rộng bộ lọc |
| Trọng số / ngưỡng | Hằng số trong code | Đưa vào cấu hình, tinh chỉnh bằng dữ liệu thật |
| Hiệu năng job | Đồng bộ toàn bộ user mỗi 10 phút | Xử lý theo trang khi dữ liệu lớn (đã ghi chú `ponytail:`) |
| OTP SMS | SĐT vẫn do user tự khai | Cần nhà cung cấp SMS; dùng lại cơ chế của `EmailOtp` |
| Chiếm email | Đăng ký trước bằng email của người khác thì chủ thật không đăng ký được (email unique) | Xóa hoặc cho đăng ký đè tài khoản `Unverified` quá 24 giờ |
| eKYC CCCD | Đã kiểm định dạng CCCD khi đăng ký; `IsVerified` vẫn chưa chặn chức năng nào | Dịch vụ eKYC (OCR + selfie/NFC); bắt buộc `IsVerified` với seller trước khi đăng bán/nhận tiền |
| Xác minh tài khoản ngân hàng | Chỉ so tên với tên KYC tự khai | Tra tên chủ tài khoản qua Napas hoặc chuyển thử |
| Một CCCD một tài khoản ở DB | Chỉ qua điểm rủi ro (CCCD trùng = 80 → từ chối) | Không đặt unique index: tài khoản trùng phải được lưu thì mới bị chấm và từ chối |
| Cookie thiết bị | Dễ né bằng cửa sổ ẩn danh | Cố ý là tín hiệu yếu; không làm fingerprint canvas |
| JMeter, k8s | Chưa chạy plan JMeter, chưa apply lên cluster thật | Chạy `tests/jmeter`, apply `k8s/` |
| `App.test.js` | Fail từ trước (Jest không import được axios ESM) | Cấu hình `transformIgnorePatterns` cho axios |

---

## 4. Setup bản mới

### 4.1 Cập nhật code và database

```bash
git pull origin main
dotnet build EbayClone.slnx
```

Có 4 migration mới. Tất cả chỉ thêm, an toàn khi rolling update.

| Migration | Thay đổi |
|---|---|
| `20261105080224_AccountLinking` | Bảng `UserIdentifiers`; cột `RiskScore`, `RiskCheckedAt`, `PayoutHoldUntil` trên `User` |
| `20261105084303_SignInHistory` | Bảng `LoginHistories` |
| `20261105090812_UniqueUserIdentifier` | Xóa dòng trùng, unique index `(UserId, Type, ValueHash)` |
| `20261105181302_EmailVerification` | Cột `EmailVerifiedAt`, `EmailOtpHash`, `EmailOtpSentAt`, `EmailOtpAttempts` trên `User` |

Áp migration bằng một trong hai cách:

```bash
# Cách 1: chạy tay
dotnet ef database update --project src/Infrastructure --startup-project src/Web

# Cách 2: app tự chạy khi khởi động
DatabaseInitialization__RunMigrationsOnStartup=true
```

### 4.2 Cấu hình (`src/Web/appsettings.json` hoặc biến môi trường)

| Khóa | Giá trị | Ghi chú |
|---|---|---|
| `BackgroundJobs:Enabled` | `true` | **Bắt buộc**: nếu không, việc duyệt user mới, monitor và xóa lịch sử đăng nhập cũ **không chạy** (file mẫu đang để `false`) |
| `Redis:ConnectionString` | ví dụ `redis:6379` | **Bắt buộc khi chạy ≥ 2 replica**: dùng cho thu hồi token, chặn dùng lại mã TOTP và khóa job. `docker-compose.yml` đã có |
| `TrustedProxyNetworks` | mặc định: loopback + dải private | Chỉ liệt kê proxy thật (nginx, ingress). **Không thêm dải IP public** |
| `InternalIps` | IP hoặc CIDR | Ai được vào app |
| `Security:EncryptionKey` | 32 byte Base64 | Đã có từ trước; cũng là khóa cho các HMAC định danh |
| `Security:AllowApiWithout2FA` | `false` | Chỉ đặt `true` khi chạy JMeter (`ALLOW_API_WITHOUT_2FA` trong `.env`) |
| `Captcha:TurnstileSecretKey` | secret key Turnstile | **Bắt buộc ở Production** để đăng ký được. Key test luôn qua: `1x0000000000000000000000000000000AA` |
| `EmailSettings:SenderEmail` / `SenderPassword` | Gmail + app password | Để gửi mã OTP thật |
| `EmailSettings:LogInsteadOfSending` | `false` | Chỉ đặt `true` khi dev không có SMTP (mã OTP in ra log) |

### 4.3 Tài khoản

- Mỗi admin đăng nhập lại và **bật 2FA** (Google Authenticator) ngay lần đăng nhập đầu.
- Tài khoản dùng cho JMeter: xem `tests/jmeter/README.md` (cần bật `ALLOW_API_WITHOUT_2FA`).

### 4.4 Kiểm tra sau khi deploy

```bash
dotnet test tests/Application.UnitTests
dotnet test tests/Application.FunctionalTests   # cần Docker (Testcontainers SQL Server)
```

Kiểm tra trên app:

1. Đăng nhập → nhập mã 2FA → vào dashboard. `document.cookie` phải rỗng (token là HttpOnly).
2. Mở một user → thấy card **Account Linking** và bảng **Recent Sign-ins**.
3. Log của API có dòng `Account monitor` hoặc `Risk check User …` (chứng tỏ background job đang chạy).
4. Chạy nhiều replica: mỗi user chờ duyệt chỉ có **1** thông báo.

### 4.5 Test các luật trên một máy

- **Giả IP:** client gửi từ localhost (proxy tin cậy) kèm header `X-Forwarded-For: <IP>`.
  - `14.161.1.1`: Việt Nam
  - `73.1.1.1`: Mỹ
  - `3.5.140.2`: AWS
- **Giả thiết bị:** mỗi context trình duyệt (Playwright `browser.newContext()`) là một thiết bị.
- Nếu test bằng IP public, cần mở `InternalIps` (ví dụ `0.0.0.0/0`, `::/0`) **chỉ trên môi trường test**.
- Code mẫu: `tests/Application.FunctionalTests/Security/SignInRulesHttpTests.cs`.
