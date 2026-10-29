# W7-01 — migration database local

Mỗi backend member dùng SQL Server/database của mình. Hoàn thành [setup](W7-01-SETUP.md) trước; giữ cùng PowerShell terminal đã đặt connection/Development và các startup flags an toàn. [Tổng kết thay đổi](SensitiveDataProtection.md). Pull code không tự migrate database hoặc cấp secrets.

**Hai bước khác nhau:** EF migration thay bảng/cột/index; CLI AES-GCM chuyển dữ liệu nhạy cảm cũ trong schema đã cập nhật. Không chạy API cũ hoặc bất kỳ writer nào trong lúc nâng cấp.

## A. Đang có database cũ

### 1. Backup và xác minh target

Dừng API/Visual Studio debug và writers local. Trong SSMS chọn đúng database của mình, Tasks → Back Up → Full; dùng file mới, bật CHECKSUM và Verify backup when finished. Không overwrite backup duy nhất. Giữ backup trước nâng cấp và key tương ứng ở nơi an toàn; restore thử trên tên/file riêng nếu cần kiểm chứng recovery. Không drop/reset DB để bỏ qua lỗi.

Trong query window của đúng database, chỉ kiểm tra:

~~~sql
SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName,
       DB_NAME() AS DatabaseName, DB_ID() AS DatabaseId;
SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
~~~

Đối chiếu server/catalog với ConnectionStrings__MyCnn từ setup. ID không giống nhau giữa máy. Nếu DB có bảng nghiệp vụ nhưng thiếu/lệch __EFMigrationsHistory: dừng, nhờ người tạo DB đối chiếu schema; không chèn history giả hoặc dùng EnsureCreated.

Nếu dữ liệu đã có gcm:v1:, phải dùng đúng EncryptionKey đã tạo nó. Nếu chỉ có plaintext và chưa có GCM thì có thể chọn key riêng; nếu có CBC logs, cần đúng Security:LegacyEncryptionKey. Không thay key của dataset đã mã hóa.

### 2. EF schema migration — GHI DATABASE

Nếu history đã có **20261028152242_ProtectSensitiveData**, bỏ qua bước này. Nếu chưa có và schema/history đã được kiểm tra, từ repo root:

~~~powershell
Push-Location src/Infrastructure
try {
    dotnet ef database update 20261028152242_ProtectSensitiveData --project . --startup-project ../Web
    if ($LASTEXITCODE -ne 0) { throw 'Schema loi: dung, khong reset/drop database.' }
} finally { Pop-Location }
~~~

Factory dùng ../Web nên cần đúng working directory trên; không tự nạp User Secrets, vì vậy vẫn phải có ConnectionStrings__MyCnn cho EF. Kiểm tra history mới, User.CccdLookupHash/index, SensitiveDataLogs và bank columns nvarchar(max). Giữ baseline counts/IDs/FK/constraints để so sau chuyển đổi.

### 3. Application dry-run — không ghi conversion

Từ repo root, không mở API thông thường:

~~~powershell
dotnet run --project src/Web --no-launch-profile -- --migrate-sensitive-data
if ($LASTEXITCODE -ne 0) { throw 'Dung: kiem tra schema/key/data; chua duoc apply.' }
~~~

CLI return trước migration startup/seeders/jobs. Kết quả có Apply=False và3 số:

| Số | Ý nghĩa |
|---|---|
| PiiFields | Số ô PII/bank chưa GCM cần chuyển; không phải số users. |
| Logs | Số sensitive logs CBC cần chuyển. |
| LookupHashes | Số CCCD lookup hashes cần thêm/sửa/xóa. |

Số tùy dataset, không dùng counts của máy người khác hoặc rehearsal. Dry-run cũng kiểm tra JSON, GCM decrypt và log integrity. Cả3 bằng0 thì **không cần apply**.

### 4. Apply — GHI DATA, chỉ khi cần chuyển

Chỉ thực hiện khi backup/key/target đúng, không còn writer khác, dry-run exit0 và counts phù hợp. Chạy đúng một lần:

~~~powershell
dotnet run --project src/Web --no-launch-profile -- --migrate-sensitive-data --apply
if ($LASTEXITCODE -ne 0) { throw 'Dung: khong tu retry apply hoac reset DB.' }
# Sau thành công, CHỈ dry-run lại:
dotnet run --project src/Web --no-launch-profile -- --migrate-sensitive-data
if ($LASTEXITCODE -ne 0) { throw 'Post-check loi: dung API va kiem tra.' }
~~~

Post-dry-run phải **0/0/0**. Xác minh số fields theo dry-run của chính DB: các field GCM decrypt đúng, hashes đúng, counts/IDs/FK/constraints giữ nguyên, log GCM có sẵn không bị mã hóa lại. Không in PII/ciphertext ra terminal hoặc lưu chúng vào test reports. Một số User audit timestamps có thể đổi theo interceptor khi User được chuyển; review đúng phạm vi.

### 5. Khi thất bại

- Schema lỗi: dừng, đọc lỗi đã redaction, kiểm tra schema/history và key/connection. Không tự downgrade, chèn history, DROP hoặc chạy lại script tùy tiện.
- Dry-run lỗi: chưa apply; xử lý schema/key/JSON/integrity trước. Key sai không được “sửa” bằng tạo key mới.
- Apply lỗi hoặc không rõ commit: giữ backup và output chỉ gồm exit/counts, dừng writers. Đọc trạng thái bằng SELECT/dry-run khi đã hiểu lỗi; không tự chạy lại --apply, reset DB hoặc rollback phá dữ liệu. Nếu cần restore, thống nhất app/schema/key tương ứng trước.
- Schema thành công nhưng conversion chưa xong là trạng thái nâng cấp một phần; chưa mở API cho đến khi kiểm chứng xong.

## B. Database mới hoàn toàn

1. Chọn tên chưa tồn tại, xác minh trong SSMS; không lấy DB cũ rồi gọi là DB mới. Không chạy DROP để làm rỗng DB.
2. Làm setup connection và tạo key riêng theo [setup](W7-01-SETUP.md), lưu/backup key trước khi seed.
3. Dùng block EF mục A.2: EF tạo DB/schema đầy đủ từ migrations. DB mới dùng key riêng được nếu không nhập dữ liệu GCM của người khác.
4. Chạy dry-run mục A.3 trên DB rỗng: kỳ vọng0/0/0; **không cần --apply**.
5. Nếu thực sự muốn dữ liệu mẫu, chỉ trong Development trên database mới/rỗng; đây là thao tác **GHI DEMO DATA**:

~~~powershell
$env:Seeding__Mode = 'Demo'
$env:BackgroundJobs__Enabled = 'false'
dotnet run --project src/Web --no-launch-profile
# Sau Ctrl+C, trả về chế độ bình thường:
$env:Seeding__Mode = 'None'
~~~

6. Demo là một transaction; lỗi rollback; có business/catalog/config/log data hoặc Identity users thì skip toàn nhóm, kể cả restart. Không repair demo dở. Sau seed, dry-run phải0/0/0. Giữ key để đọc các field GCM đã seed.

## Phát triển chức năng tiếp theo

Chỉ cần EF migration mới khi entity mapping/schema thay đổi (bảng, cột, index, FK…), không phải mỗi lần pull hoặc đổi handler/UI. Từ src/Infrastructure:

~~~powershell
# Đổi AddYourFeature thành tên thay đổi thực tế; review generated migration trước update:
dotnet ef migrations add AddYourFeature --project . --startup-project ../Web
# Sau backup và review, update DB local của mình:
dotnet ef database update --project . --startup-project ../Web
~~~

Commit migration.cs + Designer.cs + model snapshot cùng source. Database đã cutover và dry-run0/0/0 không cần chạy lại W7-01 conversion khi thêm chức năng: writers mới phải mã hóa đúng ngay khi lưu. Nếu plaintext xuất hiện trở lại, tìm writer sai trước, không tự dùng --apply để che lỗi.
