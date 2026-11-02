# W7-01 — tổng kết thay đổi và quy tắc phát triển

W7-01 bảo vệ dữ liệu nhạy cảm khi lưu/đọc và ghi audit đúng người thao tác. Gói này kèm startup seeder safety để developer dùng SQL local riêng mà không tự seed hoặc sửa dữ liệu cũ khi mở API. [Setup](W7-01-SETUP.md) → [Migration local](W7-01-README.md).

## Chức năng bổ sung và logic chỉnh sửa

| Phần | Thay đổi thực tế |
|---|---|
| Crypto | AesEncryptionService dùng AES-256-GCM có nonce ngẫu nhiên, authentication tag và envelope gcm:v1:. Key bắt buộc là Base64 của32 bytes bền vững; fail khi thiếu/sai định dạng. HMAC dùng key dẫn xuất, không SHA hash plaintext đơn thuần. |
| CCCD | User có CccdLookupHash; writers cập nhật HMAC lookup cùng CCCD; AutoReviewUsersService so lookup hash thay vì ciphertext ngẫu nhiên. |
| Audit nhạy cảm | Thêm ISensitiveDataLogger/SensitiveDataLogger, stage SensitiveDataLog cùng unit-of-work của caller, lấy actor từ IUser; không tự SaveChanges. |
| Conversion | SensitiveDataMigrationService và CLI --migrate-sensitive-data: mặc định dry-run; --apply ghi trong transaction, kiểm tra lại trước commit. CBC logs chỉ chuyển khi có đúng legacy key và integrity hợp lệ. |
| Đọc dữ liệu | Profile giải mã để mask; payout/withdrawal lists trả thông tin bảo vệ, không trả bank plaintext/ciphertext. SuperAdmin decrypt log có integrity check và audit; response no-store. |
| Logging | Bỏ request payload/chi tiết nhạy cảm khỏi các behavior, gateway/moderation/email/background logs và raw crypto error response. |
| Frontend | SensitiveLogsPage và sensitiveLogService kiểm tra integrity trước hiển thị; xóa kết quả cũ khi bắt đầu decrypt request mới hoặc đóng modal; không log decrypt response/error payload. Không có cơ chế tự xóa plaintext theo timer. |

## Thành phần bị ảnh hưởng

- Interfaces: IEncryptionService thêm VerifyIntegrity/DecryptLegacyLog; thêm ISensitiveDataLogger và UserIdentityExtensions.GetRequiredActorId; IUser có Name/IsSystem, CurrentUser cung cấp identity tương ứng.
- Entities/config: User, SensitiveDataLog, ApplicationDbContext, WithdrawalRequestConfiguration và EF model snapshot.
- Services: AesEncryptionService, SensitiveDataLogger, SensitiveDataMigrationService, AutoReviewUsersService; DI Infrastructure/Web và Program thêm/điều phối các dependency, CLI và startup gates.
- Handlers: user mock/approve/reject/status/ban/unban/penalty và profile; seller mock-order/push-payout; RunPayoutEngine; request/approve/reject withdrawals và financial lists; create/assign admin roles/admin users; broadcast, reviews và assign dispute. Nhận đầy đủ caller changes cùng interfaces/services.
- API: audit-log paging/filter binding; sensitive-log decrypt integrity/authorization/audit/no-store và error handling. Không coi các route có sẵn là API mới của W7-01.
- Frontend: src/Web/ClientApp/src/pages/SensitiveLogsPage.jsx và services/sensitiveLogService.js; targeted UI regression tests đi cùng.

## Database

Migration mới: **20261028152242_ProtectSensitiveData**; phải giữ đủ .cs, .Designer.cs và ApplicationDbContextModelSnapshot.cs.

- Tạo bảng SensitiveDataLogs, hoặc tiếp nhận bảng do seeder cũ tạo nếu đủ cột; không DROP để tái tạo. Chuẩn hóa một số audit column/nullability khi tiếp nhận.
- Thêm User.CccdLookupHash nvarchar(64), nullable; index IX_User_CccdLookupHash không unique.
- Nới WithdrawalRequests.BankName/BankAccountName/BankAccountNumber thành nvarchar(max) để chứa ciphertext.
- EF thay schema; application CLI chuyển dữ liệu cũ và tính lookup hash. Down bị chặn để tránh xóa audit/thu hẹp ciphertext; không downgrade làm rollback.

## Lỗi đã sửa

- **BUG-01 — FIXED:** AuditLogs dùng optional pageNumber=1/pageSize=20 và chuyển đủ admin/target/action/date filters; không dựa property initializer để suy ra Minimal API defaults. Regression paging/filter/malformed-number đã PASS ở lượt QA trước.
- **BUG-02 — FIXED:** từ chối actor System/missing/malformed/nonpositive trước mutation. Các handler liên quan lấy audit actor từ authenticated IUser, không tin AdminId/CreatedBy/AssignedBy trong body. Assignee dispute vẫn là dữ liệu nghiệp vụ từ request, không bị nhầm thành actor. Unit/JWT spoof regressions đã PASS.

## Startup seeder safety

| Key | Mặc định | Hành vi khi opt-in |
|---|---|---|
| DatabaseInitialization:RunMigrationsOnStartup | false | true mới cho normal startup chạy EF migration; khuyến nghị EF CLI có kiểm soát. |
| Seeding:Mode | None | System chỉ thêm Identity/domain roles thiếu, giữ custom permissions/descriptions, không tạo account. Demo chỉ Development và database mới/rỗng; có data thì skip toàn nhóm trước tạo roles/account. |
| BackgroundJobs:Enabled | false | true mới đăng ký5 hosted writer jobs: auto-review users, return escalation, seller evaluation, settlement, payout. |

Demo bao Identity bootstrap account/roles và16 seeders trong cùng transaction; lỗi rollback, restart không seed lặp, không repair dữ liệu cũ. Giá trị mode/boolean sai hoặc Demo ngoài Development fail trước startup writes. DisputesSeeder bỏ delete/regenerate disputes/messages và wallet patch; có dispute hoặc message thì skip. Request-triggered tasks giữ nguyên; các flags này không cấm API ghi nghiệp vụ.

## Ảnh hưởng khi thành viên viết chức năng mới

- Sau cutover, CCCD/VerificationDocuments/BankAccountMock/BankSnapshot và3 withdrawal bank fields là ciphertext trong DB/domain. Writer mới phải dùng IEncryptionService trước lưu; cập nhật CccdLookupHash trong cùng lần lưu CCCD. Không so CCCD bằng ciphertext, không đổi chuẩn hóa/hash riêng ở caller.
- Chỉ decrypt ngay tại boundary được phép: masking, gateway hoặc SuperAdmin log view. Không đưa plaintext/ciphertext vào DTO thông thường hoặc logs. Danh sách financial trả giá trị bảo vệ; code frontend cũ không được dựa vào bank fields đầy đủ.
- Các handler nhận thêm logger/crypto/IUser dependencies; cập nhật constructors, mocks và factories cùng revision. Actor spoof trong body không còn quyết định audit actor.
- IEncryptionService implementations/test doubles phải đáp ứng contract mới. Không thêm fallback plaintext, key mặc định hoặc plaintext JSON để làm test cũ PASS.
- Key phải giữ ổn định theo dataset; không có automatic rotation/key ring. Restore/copy GCM cần đúng key. JWT signing key là key khác, frontend không nhận cả hai.
- Logger chỉ stage log cùng nghiệp vụ; không commit “thành công” riêng khi nghiệp vụ thất bại.

## Kiểm thử và giới hạn

- Implementation đã có152 backend automated cases PASS:117 Application unit,5 Domain,30 functional SQL/HTTP/startup. Rehearsal và local main data conversion đã được kiểm chứng riêng; đó không phải production deployment.
- Kiểm tra gói Git tối giản gần nhất: bản sao HEAD + allowlist, Web/functional project build PASS (0 warnings/errors),117 Application +5 Domain +3 React tests PASS. Lượt hoàn thiện Markdown không chạy lại toàn bộ tests.
- Tests chuẩn nằm dưới tests/ và SensitiveLogsPage.test.jsx. Harness QA riêng dưới test-reports không nằm trong solution/CI chuẩn, giữ local;24 optional acceptance cases không đi trong gói Git tối giản.
- Chưa xác nhận toàn bộ frontend suite: App.test.js từng gặp Jest/Axios ESM. Chưa kiểm chứng production/load, concurrent multi-host demo startup hoặc automatic key rotation. Conversion service xử lý dataset trong memory, phù hợp đồ án; cần thiết kế batching cho dữ liệu lớn.
- UI chưa có plaintext auto-expiry timer. User Secrets không mã hóa local store; backup key ngoài Git bằng password manager.
