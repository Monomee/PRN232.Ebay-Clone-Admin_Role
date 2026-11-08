# 10. 🔐 Admin panel chỉ truy cập từ IP nội bộ hoặc xác minh 2FA

> Dự án làm **cả hai lớp**, không chọn một: IP phải nằm trong whitelist **và** tài khoản phải qua 2FA. Kẻ tấn công lấy được mật khẩu nhưng ở ngoài mạng nội bộ thì không vào được. Ở trong mạng nội bộ nhưng không có điện thoại 2FA cũng không vào được.

## Mô tả trong dự án

### Lớp 1: whitelist IP nội bộ

| Thành phần | Vị trí / hành vi |
|---|---|
| Middleware | `src/Web/Infrastructure/InternalIpMiddleware.cs`, đăng ký **rất sớm** trong `Program.cs` (trước authentication, trước cả Swagger và file tĩnh) |
| Cấu hình | `InternalIps` trong `appsettings.json`: IP đơn (`127.0.0.1`), CIDR (`172.16.0.0/12`) hoặc wildcard cũ (`192.168.1.*`). Thiếu cấu hình thì mặc định chỉ cho localhost |
| IP ngoài danh sách | Trả **403** với nội dung `Access denied. Unauthorized IP.`, ghi log `Forbidden Request from Unauthorized IP: ...` |
| Chống giả IP | Header `X-Forwarded-For` **chỉ được tin khi request đến từ proxy trong `TrustedProxyNetworks`** (nginx, ingress; mặc định loopback + dải private). Client ngoài tự gửi header này thì bị bỏ qua |
| Lỗi đã sửa | Wildcard `"10.*"` trước đây khớp nhầm `100.x.x.x`. Giờ so có dấu chấm |
| Giám sát | Trang **Network Connections** (`/active-connections`, SuperAdmin): IP nào đang truy cập, có trong whitelist không. Có thêm nút **Block** (xem file 21) |

### Lớp 2: 2FA (TOTP, Google Authenticator)

| Thành phần | Hành vi |
|---|---|
| Bắt buộc ở **API**, không chỉ ở React | Tài khoản chưa bật 2FA chỉ gọi được `/api/Auth/me`, `logout`, `enable-2fa`, `verify-2fa-setup`. Mọi API khác trả 401 |
| Gắn với bước mật khẩu | `login` đúng mật khẩu thì **chưa cấp token**. Server cấp cookie `2fa_challenge` (HttpOnly, sống 5 phút, path `/api/Auth`). `verify-2fa` lấy user từ cookie này và **bỏ qua `userId` gửi trong body** |
| Mã dùng một lần | Một mã TOTP chỉ dùng được 1 lần trong cửa sổ 30 giây. Bắt được mã cũng không đăng nhập lại được |
| Sai nhiều lần | Sai mã 2FA 5 lần trong 15 phút thì khóa tài khoản 15 phút, tính trên mọi IP |
| Token | Qua 2FA mới nhận JWT, lưu trong cookie `auth_token` (HttpOnly, Secure, SameSite=Strict). JavaScript không đọc được, nên lỗi XSS không lấy được token |
| Frontend | `ProtectedRoute.jsx`: chưa bật 2FA thì bị đẩy sang `/enable2FA` (QR code) |

Thứ tự kiểm khi request đi vào:

```
Request → [InternalIpMiddleware: IP nội bộ?] ─không→ 403
                       │có
                       ▼
          [Authentication: cookie auth_token hợp lệ? phiên còn sống?] ─không→ 401
                       │có
                       ▼
          [Đã qua 2FA?] ─không→ 401 (trừ các endpoint setup 2FA)
                       │có
                       ▼
          [Authorization: role/permission đủ?] ─không→ 403   (file 11)
```

## Để làm gì

- Trang admin điều khiển được tiền, khóa được user, đọc được dữ liệu nhạy cảm. Đây là mục tiêu số một của kẻ tấn công.
- **Whitelist IP:** thu hẹp ai có thể *chạm tới* trang admin. Botnet và người lạ trên Internet không thấy được trang login.
- **2FA:** mật khẩu bị lộ (phishing, dùng lại mật khẩu) vẫn chưa đủ để vào.
- **Gắn 2FA với bước mật khẩu:** chặn kiểu tấn công bỏ qua bước mật khẩu, gọi thẳng `verify-2fa` với `userId` của người khác rồi dò mã 6 số.

---

## Kịch bản demo

### Demo 1: IP ngoài whitelist bị chặn (1 phút)

Trong Git Bash, app đang chạy local (`https://localhost:5001`):

```bash
# 1a. Từ localhost: được vào (trang login trả 200)
curl -k -s -o /dev/null -w "%{http_code}\n" https://localhost:5001/api/Auth/captcha-config

# 1b. Giả làm IP lạ 8.8.8.8: bị chặn ngay từ lớp đầu tiên
curl -k -s -w "\n%{http_code}\n" -H "X-Forwarded-For: 8.8.8.8" https://localhost:5001/api/Auth/captcha-config
# → Access denied. Unauthorized IP.
#   403
```

- Chỉ vào console của `dotnet run`: có dòng `Forbidden Request from Unauthorized IP: 8.8.8.8`.
- Giải thích: request 1b đi từ localhost, mà localhost là proxy tin cậy, nên app tin header và coi client là `8.8.8.8`. Ngoài đời, đây đúng là cách nginx báo IP thật của người dùng.

### Demo 2: kẻ gian tự gửi `X-Forwarded-For` không lừa được (1 phút)

- Câu hỏi: "Vậy hacker tự thêm header `X-Forwarded-For: 127.0.0.1` thì sao?"
- Trả lời: header chỉ được tin khi **người gửi trực tiếp** là proxy trong `TrustedProxyNetworks`. Hacker gửi thẳng từ IP public thì header bị bỏ qua, app vẫn thấy IP thật của hacker, nên vẫn 403.
- Không giả được tình huống này trên một máy (máy mình luôn là localhost). Hãy chạy test chứng minh:

```bash
dotnet test tests/Application.FunctionalTests --filter "SpoofedForwardedFor_FromUntrustedPeer_IsRejected|ForwardedFor_FromTrustedProxy_UsesRealClientIp"
```

### Demo 3: thêm IP vào whitelist (30 giây)

1. Dừng app, chạy lại với một IP được thêm (xem README mục 5):
   ```bash
   InternalIps__6=8.8.8.8 dotnet run --project src/Web --launch-profile EbayClone.Web
   ```
2. Gọi lại lệnh 1b: giờ trả **200**.
3. Kết luận: danh sách IP nằm trong cấu hình, đổi không cần sửa code. Khi deploy thì để IP văn phòng hoặc dải VPN.

### Demo 4: bật 2FA lần đầu (1 phút)

1. Đăng nhập một admin chưa bật 2FA, ví dụ `monitor@ebay.local` nếu chưa chuẩn bị.
2. App tự chuyển sang `/enable2FA` và hiện QR. Quét bằng Google Authenticator, nhập mã.
3. Nhấn mạnh: **trước khi bật 2FA, tài khoản này không gọi được API nào**. Kiểm trong DevTools Console:
   ```js
   (await fetch('/api/Dashboard/metrics')).status   // 401 khi chưa bật 2FA
   ```

### Demo 5: luồng đăng nhập 2 bước và cookie HttpOnly (1 phút)

1. Đăng xuất. Mở DevTools > Network, đăng nhập `superadmin@ebay.local`.
2. Bấm vào request `login`:
   - Response: `requireTwoFactor: true`, **không có token**.
   - Header `Set-Cookie: 2fa_challenge=...; httponly; path=/api/Auth`.
3. Nhập mã 2FA, bấm vào request `verify-2fa`: `Set-Cookie: auth_token=...; secure; httponly; samesite=strict`.
4. Gõ trong Console: `document.cookie` cho ra **chuỗi rỗng**. JavaScript, kể cả script độc do lỗi XSS chèn vào, không đọc được token.

### Demo 6: không bỏ qua được bước mật khẩu (1 phút)

Kẻ tấn công gọi thẳng `verify-2fa` với `userId` của SuperAdmin, không có cookie `2fa_challenge`:

```bash
curl -k -s https://localhost:5001/api/Auth/verify-2fa \
  -H "Content-Type: application/json" -d '{"userId": 8, "code": "123456"}'
# → {"success":false,"errorMessage":"Phiên xác thực đã hết hạn, vui lòng đăng nhập lại"}, không có Set-Cookie auth_token
```

- Server không tin `userId` trong body. Không có cookie chứng minh đã qua bước mật khẩu thì không xử lý tiếp.
- Test: `Verify2FA_WithoutPasswordChallenge_IsRejected`.

### Demo 7: mã 2FA không dùng lại được (1 phút)

1. Mở 2 cửa sổ ẩn danh, cùng đăng nhập `superadmin@ebay.local` đến bước nhập mã.
2. Cửa sổ 1 nhập mã hiện tại: vào được.
3. Cửa sổ 2 nhập **đúng mã đó** trong cùng 30 giây: *"Mã 2FA này đã được sử dụng, vui lòng đợi mã mới"*.
4. Chờ mã mới, nhập ở cửa sổ 2: vào được.
5. Ý nghĩa: kẻ đứng sau nhìn trộm hoặc nghe lén được mã cũng không dùng lại được.
6. Test: `SameTotpCode_CannotBeUsedTwice`.

### Demo 8: sai 2FA nhiều lần thì khóa (tùy chọn, 1 phút)

- Nhập sai mã 5 lần: *"Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau 15 phút."*
- **Dùng tài khoản phụ**, không dùng superadmin đang demo.
- Gỡ khóa sớm: xóa key `ebay-count:...` trong Redis, hoặc restart app nếu không dùng Redis.

---

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Sao không chỉ dùng 2FA? | Phòng thủ nhiều lớp. Whitelist giảm bề mặt tấn công: người ngoài không dò được mật khẩu, cũng không khai thác được lỗi của trang login |
| Admin đi công tác thì sao? | Vào qua VPN công ty (IP VPN nằm trong whitelist), hoặc SuperAdmin thêm IP vào `InternalIps` |
| Mất điện thoại 2FA? | SuperAdmin đặt lại 2FA cho tài khoản đó trong DB. Chưa có màn hình tự khôi phục: ghi vào phần hạn chế |
| Chạy Docker thì sao? | Request đi qua nginx, nên `InternalIps` phải chứa gateway của mạng Docker (ví dụ `172.24.0.1`). Nếu không, mọi request bị 403 (`docs/Security-Hardening-2026-11.md`, mục 2, ghi chú 17) |

## Dự phòng (chạy cả nhóm test)

```bash
dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~SecurityHardeningHttpTests"
# JMeter: tests/jmeter/plans/10-security-ip-whitelist.jmx và 11-security-2fa.jmx (file 19)
```
