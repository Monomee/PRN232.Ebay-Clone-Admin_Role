# Database Configuration Guide

Hướng dẫn cấu hình SQL Server cho backend. Quy trình setup đầy đủ từ đầu đến cuối nằm ở [README.md mục 5](README.md#5-installation--deployment-guide-cài-đặt--triển-khai). Database cũ (trước W7-01) cần nâng cấp thì xem [docs/W7-01-README.md](docs/W7-01-README.md).

## 1. Tên connection string

Key trong code là **`MyCnn`** (`ConnectionStrings:MyCnn`). Tài liệu cũ ghi `EbayCloneDb` là sai, app sẽ không đọc được.

Mỗi thành viên dùng **SQL Server và database local của riêng mình**, không dùng chung DB của người khác.

## 2. Cách cấu hình

### Cách 1: User Secrets + biến môi trường (khuyến nghị cho local)

User Secrets lưu ngoài repo (`%APPDATA%\Microsoft\UserSecrets\`), không bị commit. Project Web đã có `UserSecretsId` nên **không cần** `dotnet user-secrets init`. Từ thư mục gốc repo:

```powershell
$env:ConnectionStrings__MyCnn = 'Server=.\SQLEXPRESS;Database=CloneEbayDB;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
@{ 'ConnectionStrings:MyCnn' = $env:ConnectionStrings__MyCnn } | ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
```

- User Secrets dùng cho lúc chạy Web (`dotnet run`, Visual Studio).
- **EF CLI (`dotnet ef ...`) không đọc User Secrets**, nên trong terminal chạy EF phải đặt biến `$env:ConnectionStrings__MyCnn` như dòng đầu. Mở terminal mới thì đặt lại.
- Không chạy `dotnet user-secrets list` khi đang chia sẻ màn hình hoặc ghi log, vì lệnh này in cả secrets (EncryptionKey, JWT key).

### Cách 2: File `appsettings` local

`src/Web/appsettings.json` và `src/Web/appsettings.Development.json` đã bị gitignore. Tạo từ file mẫu rồi sửa `ConnectionStrings:MyCnn`:

```powershell
Copy-Item src/Web/appsettings.Development.json.example src/Web/appsettings.Development.json
```

Không sửa file `*.example` để chứa thông tin cá nhân. Thứ tự ưu tiên khi chạy Development: `appsettings.json` → `appsettings.Development.json` → User Secrets → biến môi trường (cái sau ghi đè cái trước).

### Cách 3: Biến môi trường (Docker / Production)

```bash
export ConnectionStrings__MyCnn="Server=YOUR_SERVER;Database=CloneEbayDB;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True"
```

Docker Compose đã tự đặt biến này cho container API từ `SA_PASSWORD` trong file `.env`. Production có thể dùng Azure Key Vault bằng cách đặt `AZURE_KEY_VAULT_ENDPOINT`.

## 3. Connection string mẫu

| Loại SQL Server | Connection string |
|---|---|
| LocalDB (đi kèm Visual Studio) | `Server=(localdb)\mssqllocaldb;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True` |
| SQL Server Express, Windows Auth | `Server=.\SQLEXPRESS;Database=CloneEbayDB;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True` |
| SQL Server mặc định, Windows Auth | `Server=localhost;Database=CloneEbayDB;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True` |
| SQL Auth (tài khoản `sa`) | `Server=localhost;Database=CloneEbayDB;User Id=sa;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True` |
| SQL Server trong Docker (`docker-compose.yml`) | `Server=localhost,1433;Database=CloneEbayDB;User Id=sa;Password=<SA_PASSWORD trong .env>;Encrypt=True;TrustServerCertificate=True` |

`TrustServerCertificate=True` chỉ dùng cho SQL local có certificate tự ký. Không ghi mật khẩu thật vào file trong repo, Markdown hoặc chat.

## 4. Tạo database lần đầu

Startup **không tự migrate và không tự seed** (`DatabaseInitialization:RunMigrationsOnStartup=false`, `Seeding:Mode=None`). Phải tạo schema bằng EF CLI. Từ thư mục gốc repo, trong terminal đã đặt `$env:ConnectionStrings__MyCnn`:

```powershell
Push-Location src/Infrastructure
dotnet ef database update --project . --startup-project ../Web
Pop-Location
```

Lệnh tự tạo database nếu chưa có và chạy toàn bộ migrations. Kiểm tra trong SSMS có bảng `dbo.__EFMigrationsHistory`. Sau đó:
1. Chạy dry-run dữ liệu nhạy cảm, kỳ vọng `0/0/0`: `dotnet run --project src/Web --no-launch-profile -- --migrate-sensitive-data`
2. Seed dữ liệu mẫu nếu cần tài khoản demo: xem [README.md mục 5.9](README.md#59-seed-dữ-liệu-mẫu-demo).

Bước 1 và 2 cần `Security:EncryptionKey` đã có trong User Secrets ([README.md mục 5.6](README.md#56-connection-string-encryptionkey-và-jwt-key-user-secrets)).

## 5. Khi có migration mới

- **Pull code có migration mới của người khác:** chạy lại lệnh `dotnet ef database update` ở mục 4.
- **Bạn đổi entity/schema:** từ `src/Infrastructure` chạy `dotnet ef migrations add TenThayDoi --project . --startup-project ../Web`, review file sinh ra, rồi `database update`. Commit cả migration, `.Designer.cs` và `ApplicationDbContextModelSnapshot.cs`.
- Migrations nằm ở `src/Infrastructure/Data/Migrations`.

## 6. Backup và an toàn dữ liệu

- Trước khi update schema trên DB đang có dữ liệu: SSMS → chuột phải database → *Tasks → Back Up* (Full, bật *Verify backup when finished*).
- Một số cột (CCCD, tài khoản ngân hàng, log nhạy cảm) được mã hóa AES-GCM. Backup chỉ đọc được khi còn **đúng `Security:EncryptionKey`** đã dùng để mã hóa, nên lưu key trong password manager cùng với tên server/database.
- Không dùng `EnsureCreated`, không chèn tay vào `__EFMigrationsHistory`, không DROP database để sửa lỗi migration. Gặp lỗi thì xem [TROUBLESHOOTING.md](TROUBLESHOOTING.md) và [docs/W7-01-SETUP.md §7](docs/W7-01-SETUP.md#7-troubleshooting).
- Không commit connection string có mật khẩu, file `.bak` hoặc key vào Git.
