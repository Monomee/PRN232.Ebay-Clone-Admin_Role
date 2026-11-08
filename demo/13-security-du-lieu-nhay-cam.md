# 13. 💾 Dữ liệu nhạy cảm phải được log riêng (kèm mã hóa khi lưu)

Yêu cầu này có **3 phần**, phần nào cũng demo được:
- **A. Mã hóa khi lưu:** dữ liệu cá nhân và tài chính trong DB là ciphertext, không phải chữ thường.
- **B. Log riêng:** bảng `SensitiveDataLogs` tách khỏi audit log thường, và nội dung log cũng được mã hóa.
- **C. Đọc log có kiểm soát:** chỉ SuperAdmin giải mã được, có kiểm tra toàn vẹn, và chính việc giải mã cũng bị ghi log.

## Mô tả trong dự án

### A. Mã hóa khi lưu (encryption at rest)

| Thành phần | Chi tiết |
|---|---|
| Thuật toán | **AES-256-GCM** (`Infrastructure/Services/AesEncryptionService.cs`): nonce ngẫu nhiên mỗi lần, có authentication tag (sửa 1 byte là giải mã thất bại), envelope dạng `gcm:v1:...` |
| Khóa | `Security:EncryptionKey`: Base64 của 32 byte, **không có khóa mặc định**. Thiếu hoặc sai định dạng thì app không khởi động. Khóa nằm trong `.env` hoặc k8s secret, không nằm trong Git |
| Trường được mã hóa | CCCD và giấy tờ (`User.VerificationDocuments`), tài khoản ngân hàng (`User.BankAccountMock`, `BankSnapshot`), 3 trường ngân hàng của `WithdrawalRequests` |
| Tra cứu không cần giải mã | `User.CccdLookupHash` = **HMAC** của CCCD đã chuẩn hóa. Hai người trùng CCCD thì trùng HMAC, so được mà không giải mã ai |
| Định danh chống clone | Bảng `UserIdentifiers` chỉ lưu HMAC (email, SĐT, ngân hàng, thiết bị, IP...), **không lưu giá trị thật** (file 21) |
| Hiển thị | Dữ liệu chỉ được giải mã ở "biên" được phép, rồi **che bớt** (ví dụ chỉ hiện 4 số cuối tài khoản ngân hàng). DTO thường không chứa plaintext |

### B. Log riêng cho thao tác nhạy cảm

| Thành phần | Chi tiết |
|---|---|
| Bảng | `SensitiveDataLogs`, **tách khỏi** `AuditLogs` thường |
| Ghi log | `ISensitiveDataLogger` / `SensitiveDataLogger`. Log được ghi **trong cùng transaction** với nghiệp vụ: nghiệp vụ lỗi thì không có log "thành công" giả |
| Các cột | `ActorId`, `ActorUsername`, `ActorRole` (lấy từ JWT, không từ body), `Action`, `TargetEntity`, `TargetId`, `DataClassification` (`PII`, `Financial`, `SecurityCredential`, `AdminPrivilege`), `EncryptedData`, `EncryptionAlgorithm`, `KeyIdentifier`, `DataHash` (HMAC để kiểm toàn vẹn), `IpAddress`, `UserAgent` |
| Hành động đang ghi | `UpdatePaymentInfo`, `LinkBankAccount`, `BankAccountReuse`, `RequestWithdrawal`, `ApproveWithdrawal`, `RejectWithdrawal`, `PayoutStarted`, `PayoutResult`, `PayoutRejected`, `BanUser`, `UnbanUser`, `LiftRestrictions`, `ViewUserProfile`, `CreateAdminUser`, `AssignAdminRole`, `AutoReviewUser`, `AccountMonitorLink`, `AccountMonitorSpike`, `NewDevice`, `ImpossibleTravel`, `Datacenter`, `IpBlock`, `IpBlockAuto`, `IpBlockLift`, `DecryptSensitiveDataLog`... |
| Log ứng dụng (console, file) | Đã bỏ request payload, chi tiết lỗi crypto, nội dung email khỏi log. Lỗi 500 chỉ trả câu chung, chi tiết chỉ nằm ở log server |

### C. Đọc log có kiểm soát

| Thành phần | Chi tiết |
|---|---|
| Trang | **Sensitive Logs** (`/sensitive-logs`, chỉ SuperAdmin). Lọc theo phân loại, hành động; có thống kê summary |
| Giải mã | `POST /api/SensitiveLogs/{id}/decrypt` → `DecryptSensitiveLogQuery`: giải mã, rồi **so HMAC** (`VerifyIntegrity`). Ai sửa dữ liệu trong DB thì sẽ bị phát hiện |
| Giải mã cũng bị ghi | Mỗi lần giải mã sinh thêm một dòng `DecryptSensitiveDataLog`: ai đã xem dữ liệu nào, lúc nào |
| Không cache | Response giải mã có `Cache-Control: no-store`. Trình duyệt và proxy không lưu plaintext. Đóng modal là kết quả bị xóa khỏi state React |

## Để làm gì

- **Luật:** Nghị định 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân. CCCD và tài khoản ngân hàng là dữ liệu cá nhân nhạy cảm.
- **Lộ DB thì không lộ dữ liệu:** backup bị trộm hoặc SQL injection đọc được bảng `User` thì kẻ gian chỉ thấy `gcm:v1:...`. Không có khóa thì không đọc được.
- **Truy vết:** khi có sự cố (ví dụ tiền bị chuyển sai tài khoản), log riêng cho biết ai đổi tài khoản ngân hàng, ai duyệt rút tiền, ai đã xem CCCD.
- **Chống "admin tò mò":** admin xem dữ liệu nhạy cảm thì để lại dấu vết.

---

## Kịch bản demo

### Demo 1: DB chỉ chứa ciphertext (1,5 phút)

Mở SSMS / Azure Data Studio (hoặc dùng `sqlcmd`), DB `CloneEbayDB`:

```sql
SELECT TOP 5 Id, Email, LEFT(VerificationDocuments, 60) AS Kyc, LEFT(BankAccountMock, 60) AS Bank, CccdLookupHash
FROM [User] WHERE VerificationDocuments IS NOT NULL;
```

- `Kyc` và `Bank` bắt đầu bằng `gcm:v1:` và là chuỗi vô nghĩa.
- `CccdLookupHash` là chuỗi hex 64 ký tự.

```sql
SELECT TOP 10 UserId, Type, ValueHash FROM UserIdentifiers ORDER BY Id DESC;
```

- Chỉ có loại (`Email`, `Phone`, `Device`...) và HMAC, không có email hay SĐT thật.

**Điểm nhấn:** đăng ký 2 tài khoản cùng CCCD (file 21, demo B).
- Hai dòng có `VerificationDocuments` **khác nhau**, vì nonce ngẫu nhiên.
- Nhưng `CccdLookupHash` **giống nhau**.
- Nhờ vậy vẫn phát hiện trùng mà không phải giải mã.

### Demo 2: thao tác nhạy cảm sinh log riêng (1,5 phút)

1. Cửa sổ Support: **Ban** một user (lý do *"Demo log"*).
2. Seller `fashion@example.com` vào `/seller-bank-account` và cập nhật tài khoản ngân hàng.
   - Tên chủ tài khoản phải khớp tên KYC.
   - Không làm được bước này thì dùng hành động Ban là đủ.
3. Cửa sổ SuperAdmin, vào **Sensitive Logs**:
   - thấy dòng `BanUser` (phân loại `AdminPrivilege`) và `UpdatePaymentInfo` (`Financial`);
   - mỗi dòng có actor, IP, user agent, thời gian;
   - cột dữ liệu đang **bị khóa** (ciphertext).
4. Đối chiếu với SQL:
   ```sql
   SELECT TOP 3 Id, Action, DataClassification, EncryptionAlgorithm, LEFT(EncryptedData, 50) AS Data, DataHash
   FROM SensitiveDataLogs ORDER BY Id DESC;
   ```

### Demo 3: giải mã có kiểm soát (1 phút)

1. Trên Sensitive Logs, bấm giải mã dòng `UpdatePaymentInfo`.
   - Modal hiện nội dung, ví dụ tài khoản ngân hàng **chỉ 4 số cuối**.
   - Có dấu hiệu **integrity verified**.
2. Chỉ vào thông báo *"This sensitive data decryption operation has been automatically recorded..."*.
3. Refresh danh sách: có dòng mới `DecryptSensitiveDataLog`, actor là chính mình.
4. DevTools > Network > request `decrypt`: response header `Cache-Control: no-store`.

### Demo 4: phát hiện dữ liệu log bị sửa (1 phút, ăn điểm)

Giả làm kẻ gian sửa thẳng trong DB:

```sql
-- Đổi 1 ký tự cuối của dòng log mới nhất (chỉ làm trên DB demo)
UPDATE SensitiveDataLogs SET EncryptedData = LEFT(EncryptedData, LEN(EncryptedData)-2) + 'AA'
WHERE Id = (SELECT MAX(Id) FROM SensitiveDataLogs WHERE Action <> 'DecryptSensitiveDataLog');
```

- Bấm giải mã lại dòng đó: **thất bại**. Authentication tag của GCM không khớp, hoặc HMAC không khớp.
- Hệ thống không bao giờ hiện dữ liệu đã bị sửa như thể là thật.
- Nói rõ: đây là dòng log demo, xóa đi sau buổi demo.

### Demo 5: người không phải SuperAdmin không đọc được (30 giây)

Cửa sổ Support, DevTools Console:

```js
(await fetch('/api/SensitiveLogs')).status                          // 403
(await fetch('/api/SensitiveLogs/1/decrypt', {method:'POST'})).status // 403
```

### Demo 6: app không chạy khi thiếu khóa (30 giây, tùy chọn)

```bash
Security__EncryptionKey= dotnet run --project src/Web --launch-profile EbayClone.Web
```

App báo lỗi thiếu khóa và dừng. Không có chế độ "chạy tạm bằng khóa mặc định".

---

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Sao không dùng SHA-256 cho CCCD? | CCCD chỉ có 12 số, nên dò hết bằng brute-force rất nhanh. HMAC cần khóa bí mật, mất khóa thì không dò được |
| Xoay khóa (key rotation) thì sao? | Đã có cột `KeyIdentifier` = `v1` để chuẩn bị, nhưng **chưa làm** xoay khóa tự động. Ghi vào phần hạn chế |
| Dữ liệu cũ trước khi mã hóa? | CLI `--migrate-sensitive-data`: mặc định chạy thử (dry-run), thêm `--apply` thì chuyển dữ liệu trong transaction (`docs/SensitiveDataProtection.md`) |

## Dự phòng

```bash
dotnet test tests/Application.UnitTests --filter "FullyQualifiedName~SensitiveLog|FullyQualifiedName~Encryption"
dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~SensitiveLogHttpTests"
```
