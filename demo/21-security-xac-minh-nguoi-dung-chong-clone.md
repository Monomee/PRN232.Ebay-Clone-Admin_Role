# S1. Xác minh người dùng, chống tạo nhiều tài khoản (clone)

> Mở rộng cho yêu cầu 2 ("duyệt tài khoản mới"). Câu hỏi thầy hay hỏi: *"Một người tạo 10 tài khoản để tự review, hoặc bị ban xong tạo tài khoản mới thì hệ thống làm gì?"*. File này là câu trả lời đầy đủ, chia thành 9 lớp, mỗi lớp có một demo.

## Tổng quan các lớp

```
ĐĂNG KÝ                          CHẤM ĐIỂM RỦI RO (job 15s)           SAU KHI DUYỆT (monitor 10 phút)
───────────────────────          ─────────────────────────            ──────────────────────────────
A. Captcha Turnstile             C. So khớp định danh (HMAC):         G. Liên kết với tài khoản bị ban
   Rate limit 5/phút/IP             CCCD, ngân hàng, email,              → khóa đăng bán, giữ payout
   Email dùng một lần ✗             SĐT, thiết bị, địa chỉ,           H. Đăng nhập bất thường:
   Username trùng ✗                 IP, tọa độ                           thiết bị mới, datacenter,
   CCCD sai định dạng ✗          D. Tín hiệu riêng:                      di chuyển bất khả thi
   Seller < 18 tuổi ✗               IP datacenter, đăng ký             I. Giới hạn seller mới 90 ngày
   OTP email (Unverified)           hàng loạt cùng IP                     + một ngân hàng một seller
E. Blacklist IP đăng ký          → <30 duyệt / 30–79 admin / ≥80 từ chối
```

| Điểm | Kết quả |
|---|---|
| < 30 | Tự **Approved** |
| 30–79 | **PendingApproval**: vào tab **Risk Review** cho admin |
| ≥ 80 | Tự **Rejected** |

Trọng số khi trùng (trùng với tài khoản thường / trùng với tài khoản bị ban): CCCD 80/100, Bank 50/100, Email 40/60, Phone 30/60, Device 30/60, Address 15/40, IP 15/30, Geo 15/20. Tín hiệu riêng: IP datacenter +20, đăng ký hàng loạt cùng IP +30, tên chủ tài khoản ngân hàng khác tên KYC +40. Tất cả chỉnh được trong section `Risk` của cấu hình.

**Nguyên tắc riêng tư:**
- Bảng `UserIdentifiers` chỉ lưu **HMAC** của giá trị đã chuẩn hóa.
- Admin thấy "trùng *loại* SĐT với user #12", không thấy số điện thoại.
- Thông báo gửi user **không nói** trùng thông tin gì, để kẻ gian không biết mình bị lộ ở điểm nào.

## Chuẩn bị riêng cho file này

Chạy app với secret Turnstile dạng test (luôn qua), để đăng ký được bằng `curl`, cùng với các IP giả:

```bash
Captcha__TurnstileSecretKey=1x0000000000000000000000000000000AA \
InternalIps__6=14.161.1.1 InternalIps__7=73.1.1.1 InternalIps__8=3.5.140.2 \
dotnet run --project src/Web --launch-profile EbayClone.Web
```

`appsettings.Development.json` phải có `BackgroundJobs:Enabled=true` và `EmailSettings:LogInsteadOfSending=true`. Mã OTP nằm trong dòng log `LogInsteadOfSending - mail to <email>`.

Hàm tiện dụng (Git Bash), dán một lần:

```bash
reg() {  # reg <email> <username> <ip> [cccd] [phone]
  curl -k -s https://localhost:5001/api/Auth/register -H "Content-Type: application/json" -H "X-Forwarded-For: $3" \
    -d "{\"email\":\"$1\",\"username\":\"$2\",\"password\":\"secret123\",\"role\":\"Seller\",\"fullName\":\"Tran Thi B\",
         \"cccd\":\"${4:-001301001234}\",\"dateOfBirth\":\"2001-05-20\",\"phone\":\"${5:-0987654321}\",\"captchaToken\":\"demo\"}"; echo; }
otp() {  # otp <email> <mã>
  curl -k -s https://localhost:5001/api/Auth/verify-email -H "Content-Type: application/json" -d "{\"email\":\"$1\",\"code\":\"$2\"}"; echo; }
```

> **Lưu ý thiết bị:** đăng ký trên trình duyệt thì mọi tài khoản tạo từ cùng một trình duyệt (kể cả các tab ẩn danh mở cùng lúc) bị liên kết `Device`, +30 điểm. Muốn tạo tài khoản "sạch" trên UI thì đóng hết cửa sổ ẩn danh trước khi mở cửa sổ mới. `curl` không gửi cookie, nên không bị dính.

---

## A. Chặn ngay từ form đăng ký (2 phút)

| Thử | Lệnh / thao tác | Kết quả |
|---|---|---|
| Email dùng một lần | `reg x@mailinator.com demo_a1 127.0.0.1` | 400 *"Không chấp nhận email dùng một lần."* |
| CCCD bịa | `reg a2@example.com demo_a2 127.0.0.1 001999001234` | 400 *"Số CCCD không hợp lệ (12 số, mã tỉnh và năm sinh phải khớp với ngày sinh)."* Số thứ 4 và 2 số sau phải khớp năm sinh 2001 |
| Seller chưa đủ 18 | Trên UI, chọn ngày sinh 2012 | 400 *"Seller phải đủ 18 tuổi."* |
| Tự đăng ký làm admin | Gửi `"role":"SuperAdmin"` | 400 *"Chỉ đăng ký được tài khoản Buyer hoặc Seller."* |
| Username trùng | Đăng ký 2 lần cùng `demo_a3` | Lần 2 bị từ chối |
| Bot | Trên UI `/register`: widget **Cloudflare Turnstile** | Không giải captcha thì không bấm đăng ký được. Production thiếu secret thì **từ chối mọi lượt đăng ký** (thà không cho đăng ký còn hơn không có captcha) |
| Spam | Gọi `register` 6 lần trong 1 phút từ 1 IP | Lần 6 bị **429** (file 16) |

**OTP email:**
1. `reg sach1@example.com demo_sach1 127.0.0.1 001201005678` → OK, tài khoản ở trạng thái `Unverified`.
2. Đăng nhập trên UI: *"Vui lòng xác minh email trước khi đăng nhập"*.
3. Nhập sai mã 5 lần: phải xin mã mới. Xin lại mã tối đa 1 lần/phút.
4. Mã hết hạn sau 10 phút. DB chỉ lưu HMAC của mã.
5. `otp sach1@example.com <mã trong log>` → `Pending` → sau khoảng 15 giây job chấm điểm → **Approved** (sạch).
6. `verify-email` với email không tồn tại trả **cùng một câu**, nên không dò được email nào đã đăng ký.

## B. Trùng CCCD: tự từ chối (2 phút) ⭐ demo chính

```bash
reg clone1@example.com demo_clone1 127.0.0.1 079301001111 0911111111   # tài khoản gốc
otp clone1@example.com <mã>
# chờ ~15 giây → Approved
reg clone2@example.com demo_clone2 127.0.0.1 079301001111 0922222222   # cùng CCCD, khác email, khác SĐT
otp clone2@example.com <mã>
# chờ ~15 giây
```

1. Vào `/users`. `demo_clone2` bị **Rejected** tự động (CCCD trùng: 80 điểm, ≥ 80).
2. Mở chi tiết `demo_clone2`, card **Account Linking**:
   - điểm 80, lý do *trùng CCCD*, liên kết tới `demo_clone1`;
   - **không hiện số CCCD**.
3. Đăng nhập `clone2@example.com`: bị từ chối.
4. *(Nối sang file 13)* SQL:
   ```sql
   SELECT Id, Email, CccdLookupHash FROM [User] WHERE Email LIKE 'clone%';
   ```
   Hai hash **giống nhau**, dù CCCD được mã hóa với nonce khác nhau.

## C. Biến thể email và số điện thoại: không lách được (1,5 phút)

Gmail coi `tran.thi.b+shop2@gmail.com` và `tranthib@gmail.com` là **cùng một hộp thư**. Hệ thống chuẩn hóa trước khi băm. Số `0987…` được coi là bằng `+84 987…`.

```bash
reg tranthib.demo@gmail.com demo_mail1 127.0.0.1 001201007777 0987000111
otp tranthib.demo@gmail.com <mã>
reg tran.thi.b.demo+shop2@gmail.com demo_mail2 127.0.0.1 001201008888 +84987000111
otp tran.thi.b.demo+shop2@gmail.com <mã>
```

`demo_mail2` vào tab **Risk Review** với điểm 70 (Email 40 + Phone 30). Admin thấy lý do, rồi quyết định Approve hoặc Reject.

## D. Tín hiệu riêng: IP datacenter, đăng ký hàng loạt (2 phút)

**IP datacenter / VPS** (người thật hiếm khi đăng ký từ máy chủ AWS):

```bash
reg dc1@example.com demo_dc1 3.5.140.2 001201001001 0901001001 && otp dc1@example.com <mã>
```

Account Linking hiện *"IP thuộc datacenter/VPS (AWS)..."* +20. Danh sách dải IP AWS và Google được tải mới mỗi ngày.

**Đăng ký hàng loạt từ một IP** (kiểu mở cửa sổ ẩn danh liên tục để có cookie mới mỗi lần):

```bash
for n in 1 2 3 4; do reg burst$n@example.com demo_burst$n 14.161.1.1 00120100200$n 090200200$n; done
# verify cả 4 bằng otp ... (mã trong log)
```

- Tài khoản thứ 4 có tín hiệu *"3 tài khoản khác đăng ký từ cùng IP trong 24 giờ"* +30, nên vào **Risk Review**.
- IP private (Docker, localhost) không được tính, vì đó là IP của proxy chứ không phải người dùng.

## E. Blacklist IP khi đăng ký (2 phút) ⭐

1. SuperAdmin, vào **Network Connections** (`/active-connections`). Trong form **Sign-up Blocklist**, nhập `14.161.1.1`, lý do *"Spam tài khoản"*, 7 ngày, bấm **Block sign-up**.
2. Đăng ký thử:
   ```bash
   reg blocked1@example.com demo_blocked1 14.161.1.1 001201003001 0903003001
   ```
   - Kết quả: 400 *"Không thể đăng ký lúc này. Vui lòng thử lại sau hoặc liên hệ bộ phận hỗ trợ."* Câu chung chung, không nói là bị chặn vì IP.
   - Log server ghi rõ: `Sign-up refused: network 14.161.1.1/32 is blocked (IpBlock n)`.
3. Từ IP hàng xóm `14.161.1.2` vẫn đăng ký được. Đăng nhập từ `14.161.1.1` cũng **vẫn được**: chỉ chặn đăng ký, vì một IP thường có nhiều người dùng chung (4G CGNAT, wifi trường).
4. Bấm **Lift**: dòng chuyển trạng thái `Lifted` (giữ lại làm lịch sử), đăng ký lại được.
5. Giải thích **chặn tự động**:
   - 3 tài khoản dùng IP đó bị ban trong 30 ngày, hoặc 1 tài khoản bị ban dùng IP datacenter → job tự chặn 30 ngày rồi tự hết hạn;
   - IPv6 chặn cả dải `/64`;
   - admin chặn tay tối đa dải `/16`, để gõ nhầm không chặn cả một nhà mạng;
   - admin đã Lift thì job không chặn lại trên **cùng bằng chứng** cũ.
6. Mọi lần block hoặc lift ghi `SensitiveDataLog` (`IpBlock`, `IpBlockAuto`, `IpBlockLift`).

## F. Bị ban rồi tạo tài khoản mới (1,5 phút)

1. Ban `demo_mail1` (từ demo C).
2. Đăng ký tài khoản mới, CCCD khác, nhưng **cùng số điện thoại** `0987000111`:
   ```bash
   reg comeback@example.com demo_comeback 127.0.0.1 001201009999 0987000111 && otp comeback@example.com <mã>
   ```
3. Trùng SĐT với tài khoản **đã bị ban**: 60 điểm, thay vì 30 khi trùng với tài khoản thường. Tài khoản vào Risk Review, admin thấy *"trùng SĐT với tài khoản bị khóa"*.
4. **Monitor sau khi duyệt** (`AccountMonitor`, mỗi 10 phút):
   - tài khoản *đang hoạt động* bị phát hiện trùng thông tin với tài khoản vừa bị ban: khóa đăng bán 7 ngày, giữ payout 30 ngày;
   - admin gỡ được bằng **Lift restrictions**.
   - Thang xử lý: cảnh báo → khóa đăng bán → giữ payout → Suspend → Ban (bước cuối chỉ admin làm).

## G. Một tài khoản ngân hàng chỉ cho một seller (1 phút, cần 2 seller đã duyệt và bật 2FA)

1. Seller A vào `/seller-bank-account`, nhập số tài khoản `0123456789`, tên chủ tài khoản = tên KYC → OK.
2. Seller B nhập **cùng số** → 400 *"Tài khoản ngân hàng này đã được đăng ký cho một tài khoản khác."* Đồng thời ghi `SensitiveDataLog` `BankAccountReuse`.
3. Seller A nhập tên chủ tài khoản khác tên KYC → 400 (không cho rút tiền về tài khoản của người khác).
4. Seller A đổi sang tài khoản ngân hàng khác:
   - giữ payout 48 giờ;
   - gửi **email cảnh báo** cho chủ tài khoản (phòng trường hợp tài khoản bị chiếm).

Dự phòng: test `BankAccountChange_MustMatchKycName_AndHoldsPayouts`.

## H. Theo dõi đăng nhập: thiết bị mới, datacenter, di chuyển bất khả thi (2 phút, nâng cao)

Đăng nhập cùng một tài khoản (đã bật 2FA) từ Việt Nam rồi từ Mỹ trong vài phút:

```bash
for ip in 14.161.1.1 73.1.1.1; do
  curl -k -s -c /tmp/j.txt -H "X-Forwarded-For: $ip" https://localhost:5001/api/Auth/login \
    -H "Content-Type: application/json" -d '{"email":"<seller>","password":"<mk>"}' > /dev/null
  read -p "Mã 2FA cho lần từ $ip: " code
  curl -k -s -b /tmp/j.txt -H "X-Forwarded-For: $ip" https://localhost:5001/api/Auth/verify-2fa \
    -H "Content-Type: application/json" -d "{\"code\":\"$code\"}"; echo
done
```

(Mã TOTP dùng một lần, nên lần 2 phải chờ mã mới.)

Mở `/users/<id>`, bảng **Recent Sign-ins**:
- lần 1 có cờ `NewDevice` (curl không có cookie thiết bị);
- lần 2 có cờ **`ImpossibleTravel`**: hơn 13.000 km trong vài phút, tức nhanh hơn 900 km/h.
- Seller bị **giữ payout 48 giờ** và nhận email cảnh báo.
- **Không chặn đăng nhập**, chỉ gắn cờ (tránh chặn nhầm khi dùng VPN).
- Hai lần đăng nhập cùng quốc gia không bao giờ bị tính là di chuyển (dữ liệu vị trí cấp thành phố hay sai: 4G ở TP.HCM có thể hiện là Hà Nội).
- Vị trí IP lấy từ DB-IP City Lite; trang ghi nguồn "IP Geolocation by DB-IP".

## I. Giới hạn seller mới (30 giây, giải thích)

Trong 90 ngày đầu:
- tối đa 10 sản phẩm đang bán, giá ≤ 500;
- nếu tài khoản phải qua duyệt tay: 3 sản phẩm, giá ≤ 100.
- Vượt giới hạn trả 400.
- Seller hạng `TopRated` không bị giới hạn.

Mục đích: kẻ lừa đảo lọt lưới cũng không kịp "ôm tiền" nhiều. Test: `NewSeller_IsLimited_RiskySellerMore`.

---

## Chưa làm (nói thật nếu bị hỏi)

| Mục | Hiện trạng | Hướng làm (`docs/Identity-Verification-Roadmap.md`) |
|---|---|---|
| eKYC CCCD (chụp thẻ, đối chiếu khuôn mặt) | Chỉ kiểm **định dạng** CCCD: chặn số bịa, nhưng không chứng minh được thẻ có thật | FPT.AI / VNPT eKYC |
| OTP SMS | SĐT do user tự khai | Twilio / eSMS / SpeedSMS |
| Xác minh tài khoản ngân hàng | So tên với tên KYC tự khai | Napas / Casso, hoặc chuyển 1.000đ để xác minh |
| Khiếu nại khi bị ban (gửi giấy tờ) | Chưa có; user bị ban chỉ thấy lý do | Đã có thiết kế MVP |
| Cookie thiết bị | Né được bằng cửa sổ ẩn danh | Cố ý giữ là tín hiệu yếu; đăng ký hàng loạt theo IP bù lại |

## Dự phòng

```bash
dotnet test tests/Application.UnitTests --filter "FullyQualifiedName~AccountLinking|FullyQualifiedName~IpBlocklist|FullyQualifiedName~RegisterTests|FullyQualifiedName~SignInRules"
dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~AccountLinkingHttpTests|FullyQualifiedName~RegisterHttpTests|FullyQualifiedName~SignInRulesHttpTests"
```

## Dọn dẹp sau demo

- Xóa các user `demo_*` (cùng các dòng `UserIdentifiers` và `LoginHistories` của chúng).
- Lift block `14.161.1.1`.
