<div align="center">
  <h1> eBay Clone - Admin & Backend API</h1>
  <i>Hệ thống backend và nền tảng quản trị (Admin) cho dự án eBay Clone.</i>
</div>

---

## 1. Mô tả dự án
Đây là hệ thống Backend và trang quản trị (Admin Dashboard) của dự án **eBay Clone**. Dự án cung cấp các API cần thiết để vận hành một nền tảng thương mại điện tử tương tự eBay, với đầy đủ các nghiệp vụ từ phân quyền quản trị nội dung, quản lý người dùng, sản phẩm, tài chính, đến hệ thống thống kê và báo cáo phức tạp.

Dự án được triển khai dựa trên **Clean Architecture** (jasontaylordev template), đảm bảo tính mở rộng, bảo trì dễ dàng và khả năng test độc lập giữa các module.

---

## 2. Kiến trúc hệ thống
Hệ thống được thiết kế theo tiêu chuẩn **Clean Architecture** kết hợp mô hình **CQRS** (Command Query Responsibility Segregation) để phân tách rõ ràng luồng đọc/ghi dữ liệu.

```text
├── src/
│   ├── Domain/              # Chứa Entities, Value Objects, Enums, Exceptions, Domain Events
│   ├── Application/         # Chứa Business Logic, Use Cases (CQRS với MediatR), DTOs, Validators
│   ├── Infrastructure/      # Data Access (EF Core), External Services, Identity, Seeding
│   └── Web/                 # Minimal API (Endpoints), SignalR Hubs, Razor Pages & React Frontend (ClientApp)
└── tests/                   # Unit, Functional, Integration, Acceptance tests, JMeter
```

**Nguyên lý nền tảng:**
- **Domain Layer** là trung tâm, không phụ thuộc vào bất kỳ layer nào khác.
- **Application Layer** chỉ phụ thuộc vào Domain Layer.
- **Infrastructure Layer** và **Web Layer** phụ thuộc vào Application Layer.
- **CQRS**: Phân tách lệnh (Command) và truy vấn (Query) thông qua **MediatR**.

---

## 3. Danh sách chức năng (Features)
Hệ thống bao gồm các nhóm chức năng chính phục vụ việc vận hành sàn thương mại điện tử:

- **Quản lý hệ thống & Phân quyền:**
  - Đăng nhập (Authentication) & Phân quyền (Authorization).
  - Phân quyền theo Role cho đội ngũ quản trị (`SuperAdmin`, `Administrator`, `Support`, `Monitor`) và role quản trị tùy chỉnh theo từng quyền.
  - Ghi nhận lịch sử thao tác hệ thống toàn diện (`AuditLogs`).
  - Xác thực 2 lớp (2FA, TOTP) bắt buộc cho mọi tài khoản; giới hạn IP nội bộ cho trang admin; rate limiting và khóa tài khoản khi đăng nhập sai nhiều lần.
- **Quản lý Người dùng:**
  - Danh sách người dùng, xem chi tiết và lịch sử hoạt động.
  - Khóa/Mở khóa tài khoản, phê duyệt người dùng (`ApproveUser`).
- **Quản lý Sản phẩm & Danh mục:**
  - Danh mục sản phẩm (`Categories`).
  - Quản trị thông tin sản phẩm, kiểm duyệt và đánh dấu vi phạm (`Product Moderation`).
  - Giám sát các đánh giá (Reviews) của khách hàng.
- **Quản lý Tài chính & Thanh toán:**
  - Hệ thống ví (Wallet) cho người bán, quản lý số dư đóng băng và khả dụng.
  - Tự động quyết toán tiền cho người bán sau thời gian khiếu nại (`Settlement Engine`).
  - Xử lý các yêu cầu rút tiền của Seller (`Withdrawals`).
  - Công cụ chi trả hàng loạt và giám sát kết nối (`Payout Engine`).
  - Cấu hình và tính toán phí nền tảng (`PlatformFees`).
- **Quản lý Rủi ro & Khiếu nại:**
  - Xử lý các khiếu nại và yêu cầu trả hàng từ người mua (`ReturnRequests`).
  - Dashboard chuyên dụng để giải quyết tranh chấp giữa Buyer và Seller (`Disputes`).
- **Báo cáo & Thống kê:**
  - Màn hình Dashboard tổng quan cho Admin với các chỉ số real-time.
  - Thống kê chi tiết dữ liệu người dùng, doanh số và sản phẩm qua biểu đồ.
  - Xuất báo cáo hoạt động dưới dạng PDF/Excel.
- **Truyền thông & Thông báo:**
  - Gửi thông báo / tin nhắn hàng loạt trên toàn hệ thống (`Broadcasts`).
  - Cập nhật số dư ví và sự kiện theo thời gian thực (SignalR).

---

## 4. Công nghệ sử dụng
- **Framework:** .NET 10 (SDK 10.0.100+, theo `global.json`)
- **Frontend:** React 18 (Bootstrap 5, Chart.js, Recharts, SignalR Client)
- **Architecture:** Clean Architecture, CQRS (MediatR)
- **Cơ sở dữ liệu:** SQL Server (EF Core Code-First)
- **Authentication:** JWT (cookie HttpOnly), BCrypt, 2FA TOTP, Cloudflare Turnstile captcha
- **Real-time:** SignalR
- **Validation:** FluentValidation
- **Testing:** NUnit, Moq, Shouldly, Testcontainers (SQL Server), JMeter (load test)
- **Deployment:** Docker, Docker Compose, Nginx (load balancer), Kubernetes, GitHub Actions (CI/CD, GHCR)
- **UI (Admin):** React (chính), Razor Pages (trang Disputes)

---

## 5. Installation & Deployment Guide (Cài đặt & Triển khai)

Mục này dành cho **thành viên mới, setup từ máy trống với database mới**. Làm đúng thứ tự, tất cả lệnh chạy trong **PowerShell tại thư mục gốc repo** (trừ khi ghi khác). Không muốn cài .NET/Node/SQL → dùng Docker ở [mục 5.13](#513-cách-khác-chạy-bằng-docker).

> Đang có **database cũ** (đã dùng trước W7-01)? Không làm mục 5.6–5.8 mà làm theo [migration database cũ](docs/W7-01-README.md) (backup → EF migration → dry-run → apply).
> Gặp lỗi? Xem [TROUBLESHOOTING.md](TROUBLESHOOTING.md) và bảng lỗi ở [W7-01-SETUP.md §7](docs/W7-01-SETUP.md#7-troubleshooting).

### 5.1. Cài công cụ

| Công cụ | Phiên bản | Ghi chú |
|---|---|---|
| Git | bất kỳ | |
| .NET SDK | **10.0.100 trở lên** | Bắt buộc theo `global.json`. Kiểm tra: `dotnet --version` |
| dotnet-ef | 10.0.x | `dotnet tool install --global dotnet-ef --version 10.0.0` (đã có bản khác: `dotnet tool update --global dotnet-ef --version 10.0.0`) |
| Node.js + npm | 18 trở lên | Cho React (`react-scripts` 5). Kiểm tra: `node -v` |
| SQL Server | Express / Developer / LocalDB | Kèm **SSMS** để kiểm tra DB |
| App authenticator | Google/Microsoft Authenticator | Mọi tài khoản phải bật 2FA mới gọi được API |
| Docker Desktop | tùy chọn | Chỉ cần cho cách chạy Docker và functional tests |

### 5.2. Clone repo

```powershell
git clone https://github.com/Monomee/PRN232.Ebay-Clone-Admin_Role.git
cd PRN232.Ebay-Clone-Admin_Role
```

### 5.3. Restore và build

```powershell
dotnet restore
dotnet build src/Web/Web.csproj --no-restore
```

### 5.4. Tạo file cấu hình local

`appsettings.json` và `appsettings.Development.json` bị gitignore, phải tạo từ file mẫu:

```powershell
if (!(Test-Path src/Web/appsettings.json)) { Copy-Item src/Web/appsettings.json.example src/Web/appsettings.json }
if (!(Test-Path src/Web/appsettings.Development.json)) { Copy-Item src/Web/appsettings.Development.json.example src/Web/appsettings.Development.json }
```

Trong `src/Web/appsettings.json` (bản local vừa copy, **không sửa file `.example`**): nếu chưa dùng Email/AI thì để trống `EmailSettings:SenderEmail`, `EmailSettings:SenderPassword` và `OpenAI:ApiKey`. Không dùng `Jwt:Key` mẫu, mục 5.6 sẽ tạo key thật trong User Secrets.

### 5.5. Thiết lập biến môi trường cho terminal

Giữ **cùng một terminal** cho các bước 5.5 → 5.10. Mở terminal mới thì phải chạy lại block này **và** dòng `$env:ConnectionStrings__MyCnn` ở 5.6.

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DatabaseInitialization__RunMigrationsOnStartup = 'false'
$env:Seeding__Mode = 'None'
$env:BackgroundJobs__Enabled = 'false'
```

### 5.6. Connection string, EncryptionKey và JWT key (User Secrets)

Key connection string trong code là **`MyCnn`** (không phải `EbayCloneDb` như tài liệu cũ). Chọn **tên database mới chưa tồn tại**. Ví dụ Windows authentication:

```powershell
# Sửa Server cho đúng instance của bạn: .\SQLEXPRESS, localhost, (localdb)\mssqllocaldb ...
$env:ConnectionStrings__MyCnn = 'Server=.\SQLEXPRESS;Database=CloneEbayDB;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
# Lưu cho Web (EF CLI KHÔNG đọc User Secrets nên vẫn cần biến môi trường ở trên):
@{ 'ConnectionStrings:MyCnn' = $env:ConnectionStrings__MyCnn } | ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
```

Tạo `Security:EncryptionKey` (AES-GCM, Base64 32 byte) và `Jwt:Key` ngẫu nhiên. **Chỉ làm với database mới**; với DB đã có dữ liệu mã hóa phải xin đúng key cũ từ người giữ key ([W7-01-SETUP §3](docs/W7-01-SETUP.md#3-encryptionkey-và-jwtkey--không-ghi-giá-trị-vào-terminal-logs)).

```powershell
function New-RandomKey { $b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b) }
@{ 'Security:EncryptionKey' = (New-RandomKey); 'Jwt:Key' = (New-RandomKey) } | ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
```

> Nếu User Secrets đã có `Security:EncryptionKey` (máy từng setup) thì **không chạy lại** lệnh trên, vì sẽ ghi đè key và mất khả năng đọc dữ liệu đã mã hóa.
> **Backup key:** Visual Studio → chuột phải project Web → *Manage User Secrets*, lưu `Security:EncryptionKey` vào password manager kèm tên server/database. Mất key = không đọc lại được dữ liệu GCM. Không commit key, không gửi qua chat.

Web đã có `UserSecretsId` nên không cần `dotnet user-secrets init`.

### 5.7. Tạo database và schema (EF migrations)

```powershell
Push-Location src/Infrastructure
dotnet ef database update --project . --startup-project ../Web
Pop-Location
```

Lệnh này tự tạo database và toàn bộ bảng. Kiểm tra trong SSMS: database đã có bảng `dbo.__EFMigrationsHistory` và các bảng nghiệp vụ. **Không** dùng `EnsureCreated`, không chạy script SQL tay.

### 5.8. Kiểm tra dữ liệu nhạy cảm (dry-run, không ghi)

```powershell
dotnet run --project src/Web --no-launch-profile -- --migrate-sensitive-data
```

DB mới phải in `Apply=False` với **PiiFields/Logs/LookupHashes = 0/0/0**. Không cần `--apply`.

### 5.9. Seed dữ liệu mẫu (Demo)

Startup mặc định **không seed**, DB trống thì không có tài khoản nào để đăng nhập. Chỉ seed trên DB mới/rỗng, môi trường Development:

```powershell
$env:Seeding__Mode = 'Demo'
dotnet run --project src/Web --no-launch-profile
# Đợi log báo seed xong và app đang lắng nghe, rồi Ctrl+C
$env:Seeding__Mode = 'None'
```

Seed chạy trong một transaction, lỗi thì rollback; DB đã có data thì tự skip. Chạy lại dry-run 5.8 phải vẫn là 0/0/0.

### 5.10. Chạy backend

```powershell
dotnet dev-certs https --trust
$env:ASPNETCORE_URLS = 'https://localhost:5001'
dotnet run --project src/Web --no-launch-profile
```

Port 5001 khớp với `REACT_APP_HUB_BASE_URL` trong `src/Web/ClientApp/.env.development`. Đổi port thì phải đổi cả ở bước 5.11. Kiểm tra:
- Swagger UI: `https://localhost:5001/api`
- Health check: `https://localhost:5001/health`

### 5.11. Chạy frontend (terminal thứ hai)

```powershell
cd src/Web/ClientApp
npm ci
$env:ASPNETCORE_URLS = 'https://localhost:5001'        # URL backend, setupProxy.js dùng để proxy /api, /Identity, /hubs
$env:REACT_APP_HUB_BASE_URL = 'https://localhost:5001' # SignalR hubs
Remove-Item Env:ASPNETCORE_HTTPS_PORT -ErrorAction SilentlyContinue
npm start
```

Mở `https://localhost:44447`. Frontend **không cần** EncryptionKey, JWT key hay connection string.

### 5.12. Đăng nhập lần đầu và bật 2FA

Đăng nhập bằng **email** của tài khoản Demo ở [mục 8](#8-root-credentials-tài-khoản-quản-trị). Tài khoản chưa bật 2FA chỉ được gọi các API thiết lập 2FA, nên lần đầu hệ thống yêu cầu quét mã QR bằng app authenticator rồi nhập mã OTP. Sau đó đăng nhập bằng mật khẩu + OTP.

Admin bị `403 Forbidden` khi truy cập từ IP khác (LAN, VPN)? Thêm dải IP vào `InternalIps` trong `src/Web/appsettings.json` local.

### 5.13. Cách khác: chạy bằng Docker

Không cần .NET/Node/SQL trên máy, xem chi tiết ở [DOCKER.md](DOCKER.md):

```powershell
Copy-Item .env.example .env
# Điền SA_PASSWORD, JWT_KEY, ENCRYPTION_KEY (Base64 32 byte, tạo bằng New-RandomKey ở 5.6), EMAIL_* nếu dùng
docker-compose up --build
```

Ứng dụng ở `http://localhost:5000`, SQL Server ở `localhost:1433`.

### 5.14. Chạy test

```powershell
dotnet test tests/Domain.UnitTests
dotnet test tests/Application.UnitTests
# Functional tests cần Docker đang chạy (tự dựng SQL container riêng):
dotnet test tests/Application.FunctionalTests
# Frontend:
cd src/Web/ClientApp; npm.cmd test -- --watchAll=false
```

### 5.15. Khi phát triển chức năng mới có đổi schema

```powershell
cd src/Infrastructure
dotnet ef migrations add TenThayDoi --project . --startup-project ../Web
# Review file migration sinh ra, rồi:
dotnet ef database update --project . --startup-project ../Web
```

Commit cả `*.cs`, `*.Designer.cs` và `ApplicationDbContextModelSnapshot.cs`. Pull code có migration mới của người khác thì chạy lại lệnh `database update` (nhớ đặt `$env:ConnectionStrings__MyCnn`). Quy tắc mã hóa dữ liệu nhạy cảm cho code mới: [SensitiveDataProtection.md](docs/SensitiveDataProtection.md).

### 5.16. Triển khai lên VPS (Docker Compose)

CD (`.github/workflows/cd.yml`) tự build và push image `ghcr.io/monomee/ebay_clone_adminrole:latest` mỗi khi push vào `main`. CI (`ci.yml`) chạy build + unit/functional tests cho mỗi push và PR vào `main`. Trên VPS (Linux đã cài Docker và Nginx):

```bash
sudo mkdir -p /opt/ebay-admin/nginx && cd /opt/ebay-admin
# Copy từ repo lên VPS: docker-compose.vps.yml, nginx/nginx.vps.conf, .env.vps.example
cp .env.vps.example .env
nano .env   # điền SA_PASSWORD, JWT_KEY, ENCRYPTION_KEY (openssl rand -base64 32), EMAIL_*, TURNSTILE_*
docker compose -f docker-compose.vps.yml pull
docker compose -f docker-compose.vps.yml up -d
docker compose -f docker-compose.vps.yml logs -f api1
```

- **Lần đầu** (DB trống): để `RUN_MIGRATIONS=true`, `ASPNETCORE_ENVIRONMENT=Development`, `SEEDING_MODE=Demo` để tạo schema và tài khoản. Khi đã có dữ liệu, đổi về `ASPNETCORE_ENVIRONMENT=Production`, `SEEDING_MODE=None`, `ENABLE_SWAGGER=false` rồi `up -d` lại.
- Nginx của host: copy `nginx/ebay.vps.conf` vào `/etc/nginx/conf.d/ebay.conf`, sửa `server_name`, `sudo nginx -t && sudo systemctl reload nginx`. App chạy ở `127.0.0.1:5000`, nên có HTTPS (Let's Encrypt) để cookie đăng nhập được gắn cờ `Secure`.
- Cập nhật phiên bản mới: `docker compose -f docker-compose.vps.yml pull && docker compose -f docker-compose.vps.yml up -d`.
- Backup `ENCRYPTION_KEY` cùng volume DB. Mất key thì không đọc lại được dữ liệu đã mã hóa.

Cấu hình nhiều instance (2 API + Nginx load balancer + Redis) nằm ở `docker-compose.prod.yml`, chạy giống trên với `-f docker-compose.prod.yml` và file `.env` từ `.env.example`.

### 5.17. Triển khai Kubernetes (rolling update, zero-downtime)

```bash
cp k8s/secret.example.yaml k8s/secret.yaml                                  # điền secrets, không commit
cp k8s/data-protection-secret.example.yaml k8s/data-protection-secret.yaml
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml -f k8s/data-protection-secret.yaml
kubectl apply -f k8s/db-statefulset.yaml -f k8s/redis-deployment.yaml
kubectl apply -f k8s/api-deployment.yaml -f k8s/api-service.yaml
kubectl apply -f k8s/nginx-config.yaml -f k8s/nginx-deployment.yaml -f k8s/nginx-service.yaml
```

Chi tiết rolling update và rollback: [docs/RollingDeployment.md](docs/RollingDeployment.md), demo: [demo/20-zero-downtime-k8s.md](demo/20-zero-downtime-k8s.md).

---

## 6. Source Code

- **Repository:** https://github.com/Monomee/PRN232.Ebay-Clone-Admin_Role
- **Nhánh:** `main` là nhánh ổn định (CI/CD chạy trên nhánh này). Mỗi thành viên làm trên nhánh riêng rồi tạo Pull Request vào `main`.
- **Solution:** `EbayClone.slnx`, mở bằng Visual Studio 2022 17.14+ / Rider / VS Code.

```text
├── src/
│   ├── Domain/            # Entities, Enums, Constants (Roles, Policies), Domain Events
│   ├── Application/       # Use case theo feature (CQRS: Commands/Queries + Validators + DTOs)
│   ├── Infrastructure/    # EF Core (Data/Migrations, Data/Seeders), mã hóa, email, background jobs
│   └── Web/
│       ├── Endpoints/     # Minimal API, mỗi file là một nhóm /api/{Tên}
│       ├── Hubs/          # SignalR hubs
│       ├── Infrastructure/# Middleware: IP nội bộ, session/2FA, rate limit, security headers
│       ├── Pages/         # Razor Pages (Admin/Disputes)
│       └── ClientApp/     # React 18 (pages/, components/, services/)
├── tests/                 # Domain/Application unit tests, Application functional tests (Testcontainers),
│                          # Infrastructure integration, Web acceptance, jmeter (load test)
├── docs/                  # Tài liệu kỹ thuật (W7-01 setup/migration, bảo mật, dispute, rolling deploy)
│   └── documents/         # Tài liệu đặc tả SRS, SDS (.docx)
├── demo/                  # Kịch bản demo từng chức năng (01–22)
├── k8s/, nginx/           # Manifest Kubernetes và cấu hình Nginx
├── scripts/               # Script demo (lockout, rolling update, rate limit)
└── docker-compose*.yml    # Local, prod (2 API + LB), VPS, load test
```

Module chính trong `src/Application`:
- `Authentication`: đăng ký, đăng nhập, 2FA, xác minh email.
- `Users` / `Sellers` / `AdminRoles` / `SystemAdmin`: tài khoản, duyệt seller, role quản trị tùy chỉnh, chặn IP.
- `Products` / `Categories` / `Reviews`: sản phẩm, kiểm duyệt vi phạm, đánh giá.
- `Orders` / `ReturRequests` / `Disputes` (trong `Admin`): đơn hàng, hoàn trả, tranh chấp.
- `Financials` / `Payouts` / `Withdrawals` / `PlatformFees`: ví, quyết toán, chi trả, phí sàn.
- `Dashboard` / `Stats` / `Reports`: số liệu tổng hợp và báo cáo.
- `Broadcasts` / `Notifications` / `AuditLogs` / `SensitiveLogs`: thông báo, nhật ký thao tác, nhật ký dữ liệu nhạy cảm.

Tài liệu đặc tả (tiếng Anh) nằm trong [`docs/documents/`](docs/documents/):
- [`eBay_Clone_Admin_SRS_v1.2.docx`](docs/documents/eBay_Clone_Admin_SRS_v1.2.docx): Software Requirement Specification (yêu cầu, context diagram, use case diagram, use case specification).
- [`eBay_Clone_Admin_SDS_v1.2.docx`](docs/documents/eBay_Clone_Admin_SDS_v1.2.docx): Software Design Specification (kiến trúc, ERD, state diagram, thiết kế module, API, bảo mật, vận hành).
- [`eBay Admin Clone – Quản lý dự án (Tuần 1–9).xlsx`](<docs/documents/eBay Admin Clone – Quản lý dự án (Tuần 1–9).xlsx>): kế hoạch và phân công công việc theo tuần.

---

## 7. API Documentation

- **Swagger UI (NSwag):** `https://localhost:5001/api` (local) hoặc `http://localhost:5000/api` (Docker). Bật mặc định ngoài Production; Production cần `Features:Swagger=true`.
- **Xác thực:** JWT lưu trong cookie HttpOnly `auth_token`. Swagger cùng origin nên chỉ cần gọi `login` → `verify-2fa` ngay trong Swagger là các request sau tự mang cookie. Client khác có thể gửi header `Authorization: Bearer <token>`.
- **Bắt buộc 2FA:** tài khoản chưa bật 2FA chỉ gọi được `/api/Auth/me`, `logout`, `enable-2fa`, `verify-2fa-setup`.
- **Rate limit:** login/register/verify ~5 request/phút/IP, API thường 60/phút, API đã đăng nhập 200/phút/user. Vượt giới hạn trả `429`.
- **Lỗi:** trả về dạng ProblemDetails (`400` validation, `401` chưa đăng nhập, `403` không đủ quyền hoặc IP ngoài `InternalIps`, `404`).

| Nhóm | Endpoint chính | Quyền |
|---|---|---|
| `/api/Auth` | `POST login`, `verify-2fa`, `register`, `verify-email`, `resend-email-code`, `logout`, `enable-2fa`, `verify-2fa-setup`; `GET me`, `captcha-config` | Public / đã đăng nhập |
| `/api/Dashboard` | `GET metrics` | ViewDashboard |
| `/api/Stats` | `GET dashboard`, `reports/orders`, `reports/revenue`, `reports/users` | ViewDashboard / ViewReports |
| `/api/Users` | `GET` danh sách, `{id}`, `summary`, `seller-metrics`; `POST {id}/approve`, `reject`, `ban`, `unban`, `lift-restrictions`, `evaluate-sellers`; `PUT {id}/status`, `seller-level-criteria` | ManageUsers |
| `/api/Products` | `GET {id}`, `seller/{sellerId}`, `managed`, `managed/summary`, `managed/{id}/violation-details`; `POST reports`, `managed/{id}/resolve-violation`; `PUT {id}`; `DELETE {id}` | Đã đăng nhập / ManageProducts |
| `/api/Categories` | `GET` | Public |
| `/api/reviews` | Xem review bị báo cáo, thống kê, feedback của seller; báo cáo, trả lời, đổi trạng thái review | ModerateReviews |
| `/api/Orders` | `GET`, `summary`, `{id}` | ManageOrders |
| `/api/AdminReturnRequests` | `GET {id}`, `{id}/messages`; `POST {id}/approve`, `reject`, `freeze`, `adjudicate`, `return-label` | ManageOrders |
| `/api/Disputes` | `GET`, `statistics`, `{id}`; `POST {id}/assign`, `{id}/resolve` | ManageDisputes |
| `/api/Financials` | `GET fees`, `settlement-orders`, `pending/{sellerId}`, `reports/revenue`, `payouts/config`, `payouts/history`, `payouts/exceptions`; `PUT fees`, `payouts/config`; `POST trigger-settlement`, `settle-order/{id}`, `payouts/run`, `payouts/{id}/release-hold` | Administrator, SuperAdmin |
| `/api/Wallets` | `GET` | Administrator, SuperAdmin |
| `/api/Account` | `PUT bank-account` | Seller |
| `/api/Broadcasts` | `GET`, `GET active`, `POST`, `POST schedule`, `POST {id}/read` | ManageBroadcasts (gửi) / đã đăng nhập (đọc) |
| `/api/AdminRoles` | `GET/POST roles`, `PUT roles/{id}`, `GET/POST users`, `POST users/assign` | ManageAdminRoles |
| `/api/AuditLogs` | `GET`, `summary`, `admin/{adminId}`, `entity/{type}/{id}` | ViewAuditLogs |
| `/api/sensitive-logs` | `GET`, `summary`, `POST {id}/decrypt` | SuperAdmin |
| `/api/SystemAdmin` | `GET active-ips`, `ip-blocks`; `POST ip-blocks`, `ip-blocks/{id}/lift` | SuperAdmin |
| `/api/Mocking` | `POST generate-mock-users`, `generate-seller-order`, `generate-mock-dispute`, `generate-mock-return-request`, `generate-mock-defect`, `accelerate-settlement`, `push-payout` | SuperAdmin (công cụ demo) |

**SignalR hubs:** `/hubs/dispute` (tin nhắn tranh chấp), `/hubs/notifications` (broadcast, thông báo), `/hubs/seller` (số dư ví seller). Health check: `/health`.

---

## 8. Root Credentials (Tài khoản quản trị)

Tài khoản chỉ được tạo khi seed **Demo** (mục 5.9, hoặc `SEEDING_MODE=Demo` khi deploy). `Seeding:Mode=None/System` không tạo tài khoản nào. Đăng nhập bằng **email**:

| Vai trò | Email | Mật khẩu | Ghi chú |
|---|---|---|---|
| **SuperAdmin (root)** | `superadmin@ebay.local` | `Admin123!` | Toàn quyền, gồm role, audit/sensitive logs, payout, IP |
| Administrator | `admin@ebay.local` | `Admin123!` | Tài chính, ví, đơn hàng |
| Support | `support@ebay.local` | `Admin123!` | Người dùng, sản phẩm, review, tranh chấp, hoàn trả, broadcast |
| Monitor | `monitor@ebay.local` | `Admin123!` | Chỉ xem Dashboard |
| Seller | `fashion@example.com` | `Password123!` | Tài khoản người bán mẫu |
| Buyer | `john@example.com` | `Password123!` | Tài khoản người mua mẫu |

- Lần đăng nhập đầu, mỗi tài khoản phải bật 2FA (mục 5.12). Mã 2FA gắn với điện thoại của người quét đầu tiên, nên khi demo theo nhóm hãy thống nhất ai giữ tài khoản SuperAdmin.
- Đây là mật khẩu demo công khai. Môi trường public phải đổi mật khẩu ngay sau khi seed (hoặc tạo admin mới qua *Admin Roles* rồi khóa tài khoản demo).
- Nhập sai mật khẩu nhiều lần sẽ tạm khóa tài khoản (account lockout), đợi hết thời gian khóa rồi thử lại.

---

## 9. End-user Manual (Hướng dẫn người dùng: Buyer / Seller)

**Đăng ký** (`/register`)
1. Chọn loại tài khoản **Buyer** hoặc **Seller**, nhập email (không chấp nhận email dùng một lần), mật khẩu ≥ 8 ký tự gồm chữ và số, số điện thoại.
2. Seller phải nhập thêm họ tên, ngày sinh (đủ 18 tuổi) và số CCCD hợp lệ.
3. Hoàn thành captcha (Cloudflare Turnstile, nếu server đã cấu hình), bấm đăng ký.
4. Nhập mã xác minh được gửi qua email. Tài khoản chuyển sang chờ duyệt; Seller cần admin phê duyệt trước khi bán hàng.

**Đăng nhập** (`/login`)
1. Nhập email + mật khẩu.
2. Lần đầu: hệ thống chuyển sang `/enable2FA`, quét QR bằng Google/Microsoft Authenticator và nhập mã 6 số để kích hoạt.
3. Các lần sau: nhập mã 6 số từ app sau khi nhập mật khẩu.
4. Đăng xuất bằng menu tài khoản; phiên bị thu hồi ngay trên server.

**Buyer**
- Xem trang chủ sản phẩm (`/home`) và chi tiết sản phẩm (`/product/:id`), gồm đánh giá của người mua khác.
- Báo cáo review vi phạm (spam, xúc phạm...) ngay trên trang chi tiết; admin sẽ kiểm duyệt.

**Seller**
- **Your products** (`/seller-products`): xem danh sách sản phẩm và trạng thái (Active / Inactive / Pending Review); bấm **Add New Product** để đăng sản phẩm mới (tên, giá, Category ID, mô tả).
- **Payout bank account** (`/seller-bank-account`): khai báo tài khoản ngân hàng nhận tiền (số tài khoản được mã hóa và hiển thị dạng che).
- Tiền bán hàng nằm ở số dư chờ (pending) cho đến khi hết thời gian khiếu nại, sau đó được quyết toán sang số dư khả dụng và chi trả theo lịch của Payout Engine. Số dư cập nhật real-time.
- Bị khóa/hạn chế tài khoản: liên hệ bộ phận Support.

---

## 10. Administrator Manual (Hướng dẫn quản trị)

Admin chỉ truy cập được từ IP trong `InternalIps` (mặc định localhost và mạng nội bộ). Menu bên trái chỉ hiện các trang role của bạn được phép.

| Trang | Đường dẫn | Role | Việc cần làm |
|---|---|---|---|
| Dashboard | `/dashboard` | SuperAdmin, Support, Monitor | Xem chỉ số doanh thu, đơn hàng, người dùng theo ngày/tháng/quý; nút xuất **Excel/PDF** |
| Users | `/users`, `/users/:id` | SuperAdmin, Support | Tìm, lọc người dùng; xem chi tiết; **Approve/Reject** tài khoản chờ duyệt; **Ban/Unban**, gỡ hạn chế |
| Sellers | `/sellers` | SuperAdmin, Support | Xem chỉ số và cấp độ seller, chỉnh tiêu chí xếp hạng, chạy đánh giá lại |
| Product moderation | `/products` | SuperAdmin, Support | Xem sản phẩm bị báo cáo/AI gắn cờ, xem chi tiết vi phạm, chọn **Delete & Warn**, **Hide Product**, **Ignore Report** hoặc **Restore Product** |
| Reviews | `/review-monitoring` | SuperAdmin, Support | Xem review bị báo cáo, đổi trạng thái (ẩn/hiện), thống kê |
| Orders | `/orders` | SuperAdmin, Support, Administrator | Tra cứu đơn hàng và tổng hợp |
| Return requests | `/return-requests` | SuperAdmin, Support | Duyệt/từ chối yêu cầu trả hàng, tạo nhãn trả hàng, đóng băng tiền, phán quyết |
| Disputes | `/disputes`, `/disputes/dashboard` | SuperAdmin, Support | Nhận (assign) tranh chấp, chat real-time với các bên, ra phán quyết; xem thống kê |
| Broadcasts | `/broadcasts` | SuperAdmin, Support | Gửi ngay hoặc hẹn giờ thông báo qua kênh Email hoặc In-App tới nhóm người dùng |
| Payout engine | `/payout-engine` | SuperAdmin | Cấu hình lịch chi trả, chạy payout, xử lý ngoại lệ, gỡ hold; xem số dư chờ của seller |
| Admin roles | `/admin-roles` | SuperAdmin | Tạo role quản trị tùy chỉnh với tập quyền, tạo tài khoản admin, gán role |
| Audit logs | `/audit-logs` | SuperAdmin | Lịch sử mọi thao tác của admin, lọc theo admin hoặc đối tượng |
| Sensitive logs | `/sensitive-logs` | SuperAdmin | Xem nhật ký truy cập dữ liệu nhạy cảm; giải mã có kiểm tra toàn vẹn, mỗi lần giải mã đều được ghi lại |
| Active connections | `/active-connections` | SuperAdmin | Xem IP đang truy cập, chặn IP và gỡ chặn |
| Simulation toolbox | `/mock` | SuperAdmin | Sinh dữ liệu demo (user, đơn, tranh chấp, hoàn trả), tăng tốc quyết toán. **Chỉ dùng khi demo** |

Quy trình thường gặp:
1. **Duyệt seller mới:** Users → lọc trạng thái chờ duyệt → mở chi tiết, kiểm tra thông tin → Approve hoặc Reject kèm lý do.
2. **Xử lý tranh chấp:** Disputes → chọn tranh chấp mở → Assign cho mình → trao đổi qua khung chat → Resolve (hoàn tiền buyer hoặc giữ cho seller).
3. **Cấp quyền admin mới:** Admin roles → tạo role và chọn quyền → tạo user admin → gán role. Người được cấp đăng nhập và tự bật 2FA.

Các kịch bản demo chi tiết từng chức năng: [demo/README.md](demo/README.md).

---

## 11. Hướng phát triển thêm
- Tích hợp thêm Payment Gateway thực tế (VNPay, Stripe, PayPal, v.v.)
- Hoàn thiện hệ thống Chat / Messaging real-time giữa Buyer và Seller.
- Bổ sung ElasticSearch cho phần tìm kiếm sản phẩm để tăng hiệu năng.
- Bổ sung Caching (Redis) ở các query thống kê để giảm tải database.

---
> *Template dự án gốc thuộc về [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/CleanArchitecture).*
