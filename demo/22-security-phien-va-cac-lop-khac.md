# S2. Phiên đăng nhập, khóa tài khoản, header bảo mật, secret

> Các lớp bảo mật "chạy ngầm" không gắn với một trang cụ thể. Mỗi mục có một demo ngắn (30 giây – 1 phút). Nên chọn 3–4 mục, tùy thời gian.

## Mô tả trong dự án

| # | Lớp | Cách làm | Vị trí |
|---|---|---|---|
| 1 | Token không lộ cho JavaScript | JWT nằm trong cookie `auth_token`: **HttpOnly**, **Secure**, **SameSite=Strict**, sống 60 phút. Body của login không trả token | `AuthEndpoints.cs` |
| 2 | Chống CSRF | `SameSite=Strict`: trang web khác không gửi kèm cookie khi gọi API của mình | |
| 3 | Kiểm phiên mỗi request | `SessionValidation.OnTokenValidated`. Token còn hạn vẫn bị từ chối khi: user bị **ban/reject**, **role đổi**, **quyền đổi**, chưa bật 2FA, hoặc token không có `jti` | `Web/Infrastructure/SessionValidation.cs` |
| 4 | Đăng xuất là thu hồi | Logout ghi `jti` vào danh sách thu hồi (Redis, hoặc bộ nhớ nếu không có Redis). Token cũ dùng lại bị 401 | `SecurityCache.IsRevokedAsync` |
| 5 | Khóa theo tài khoản | Sai mật khẩu 10 lần hoặc sai 2FA 5 lần trong 15 phút: khóa 15 phút, trên mọi IP (file 16) | `SecurityCache` |
| 6 | Header bảo mật | `Content-Security-Policy` (`script-src 'self'`, cho phép Turnstile), `X-Content-Type-Options: nosniff`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`; tắt header `Server`; HSTS ở Production | `SecurityHeadersMiddleware.cs` |
| 7 | Khóa JWT an toàn | `JwtKeyPolicy`: app **không khởi động** nếu `Jwt:Key` ngắn hơn 32 byte, hoặc là khóa đã từng bị lộ trong lịch sử Git (so bằng SHA-256, không lưu khóa trong code) | `JwtKeyPolicy` |
| 8 | Secret không nằm trong repo | `appsettings.json`, `.env`, `k8s/secret.yaml` nằm trong `.gitignore`; trong repo chỉ có file `*.example`. Secret cũ từng bị lộ đã được **đổi** | `.gitignore` |
| 9 | Tắt công cụ dev ở Production | Swagger và `/api/Mocking/*` tự tắt khi chạy Production (bật lại có chủ đích bằng `Features:*`). Endpoint mock đụng tới tiền (`accelerate-settlement`, `push-payout`) chỉ SuperAdmin. `AllowApiWithout2FA` và `LogInsteadOfSending` bật ở Production thì app không khởi động | `Program.cs`, DI |
| 10 | Lỗi không lộ chi tiết | Exception không lường trước trả 500 với câu chung. Stack trace và chi tiết DB chỉ nằm trong log server | Exception handler |
| 11 | Không tin dữ liệu client | Seller khi đăng sản phẩm lấy từ JWT, bỏ qua `SellerId` trong body; actor của audit log lấy từ JWT; `Priority` của report do server quyết định | Các handler |
| 12 | Hết phiên ở frontend | API trả 401 thì `sessionGuard.js` đưa về `/login` | `ClientApp/src/services/sessionGuard.js` |

## Để làm gì

- Mật khẩu và 2FA chỉ bảo vệ **lúc đăng nhập**.
- Các lớp trên bảo vệ **sau khi đã đăng nhập**:
  - token bị trộm qua lỗi XSS;
  - nhân viên bị đuổi việc nhưng token còn hạn;
  - tấn công CSRF;
  - lộ cấu trúc hệ thống qua thông báo lỗi;
  - lộ secret qua GitHub.

---

## Kịch bản demo

### 1. Cookie HttpOnly: JavaScript không lấy được token (30 giây)

Đăng nhập, mở DevTools Console:

```js
document.cookie        // "" : không thấy auth_token
```

Mở tab **Application → Cookies**: `auth_token` có tick **HttpOnly**, **Secure**, `SameSite = Strict`.

### 2. Đăng xuất là token chết thật (1 phút) ⭐

1. Đăng nhập. Trong **Application → Cookies**, copy giá trị `auth_token`.
2. Ở terminal, dùng lại token đó:
   ```bash
   T='<dán token>'
   curl -k -s -o /dev/null -w "%{http_code}\n" -b "auth_token=$T" https://localhost:5001/api/Auth/me   # 200
   ```
3. Trên giao diện, bấm **Logout**.
4. Chạy lại đúng lệnh curl: **401**.
   - Token vẫn còn hạn 60 phút, nhưng `jti` của nó đã nằm trong danh sách thu hồi.
   - Không có lớp này thì ai sao chép được token vẫn dùng tiếp được sau khi chủ tài khoản đăng xuất.

### 3. Ban là phiên chết ngay (30 giây)

Như demo ở file 02, phần B, bước 3: seller đang mở trang, admin ban, seller bấm bất kỳ đâu → bị đưa về `/login`.

Test: `ChangedAccount_InvalidatesExistingToken`.

### 4. Header bảo mật (30 giây)

```bash
curl -k -s -I https://localhost:5001/ | grep -iE "content-security|x-frame|x-content-type|referrer-policy|permissions-policy|^server"
```

- Có đủ các header bảo mật.
- **Không có** header `Server: Kestrel`, nên kẻ tấn công không biết server chạy công nghệ gì.
- Giải thích CSP: kẻ gian chèn được `<script src="https://evil.com/x.js">` thì trình duyệt cũng từ chối tải. Thử ngay trong Console:
  ```js
  const s = document.createElement('script'); s.src = 'https://example.com/x.js'; document.body.appendChild(s);
  ```
  Console báo lỗi `Refused to load the script ... Content Security Policy`.

### 5. App từ chối khóa JWT yếu (30 giây)

```bash
Jwt__Key=ngan-qua dotnet run --project src/Web --launch-profile EbayClone.Web
# → app dừng, báo Jwt:Key quá ngắn (< 32 byte)
```

Khóa cũ từng bị commit lên GitHub cũng bị từ chối, kể cả ở môi trường dev, với thông báo `Jwt:Key is a key that was published in the repository...`.

### 6. Không có secret trong repo (30 giây)

```bash
git ls-files | grep -iE "appsettings.json$|\.env$|secret.yaml$"     # rỗng (chỉ có *.example)
git check-ignore -v src/Web/appsettings.json .env k8s/secret.yaml
```

Kể lại sự cố: `k8s/secret.yaml` (chứa mật khẩu SA `123456`, mật khẩu ứng dụng Gmail, JWT key) từng nằm trong repo public.
- Cách xử lý: xóa file khỏi repo, **đổi toàn bộ** secret, và thêm `JwtKeyPolicy` để khóa cũ không bao giờ chạy lại được.
- Xóa khỏi lịch sử Git là không đủ: ai đã clone repo thì vẫn còn bản cũ, nên bắt buộc phải đổi secret.

### 7. Không giả mạo được seller (30 giây, chạy test)

```bash
dotnet test tests/Application.FunctionalTests --filter "CreateProduct_IgnoresSellerIdFromBody|CreateProduct_Anonymous_IsRejected"
```

Seller A gửi `"sellerId": <id seller B>` để đăng sản phẩm dưới tên B. Server bỏ qua giá trị đó và lấy seller từ JWT.

### 8. Lỗi 500 không lộ chi tiết (30 giây, chạy test)

```bash
dotnet test tests/Application.FunctionalTests --filter "UnexpectedException_Returns500WithoutDetails"
```

Client chỉ nhận câu chung chung. Stack trace và tên bảng chỉ có trong log server.

### 9. Swagger và Mock tắt ở Production (30 giây, chạy test)

```bash
dotnet test tests/Application.FunctionalTests --filter "Swagger_SwitchedOffByConfig|Mocking_MoneyEndpointsSuperAdminOnly_AndSwitchedOffByConfig|AllowApiWithout2FA_LoadTestSwitch"
```

---

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Kiểm DB ở mỗi request có chậm không? | Một query nhỏ theo khóa chính. JMeter dashboard vẫn đạt p95 khoảng 50 ms với 100 thread (file 19) |
| Sao không dùng refresh token? | Phiên 60 phút là đủ cho trang admin. Hết hạn thì đăng nhập lại (có 2FA). Ít thành phần hơn thì ít lỗ hổng hơn |
| Mất Redis thì danh sách token thu hồi ra sao? | Chạy 1 instance thì dùng bộ nhớ. Chạy nhiều instance thì bắt buộc có Redis (`docs/Security-Hardening-2026-11.md`, mục 4.2) |

## Dự phòng (cả nhóm)

```bash
dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~SecurityHardeningHttpTests|FullyQualifiedName~SecurityReviewHttpTests"
```
