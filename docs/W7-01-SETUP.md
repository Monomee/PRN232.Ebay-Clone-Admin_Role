# W7-01 — setup backend và frontend

Đọc theo thứ tự: **setup này → [migration local](W7-01-README.md) → [tổng kết/quy tắc phát triển](SensitiveDataProtection.md)**. Mỗi backend member dùng database local riêng. Không nhận secret qua Git/chat/screenshots; frontend không cần EncryptionKey, SQL credentials hoặc JWT signing secret.

## 1. Công cụ và config local

Windows PowerShell5.1+ hoặc PowerShell7; .NET10 SDK theo global.json, Node/npm theo frontend, SQL Server local/SSMS. Docker chỉ cần cho SQL functional tests. Từ repo root:

~~~powershell
dotnet --version
dotnet restore
dotnet build src/Web/Web.csproj --no-restore
dotnet ef --version
# Chỉ cài nếu chưa có, hoặc dùng dotnet tool update nếu đang dùng bản không tương thích:
# dotnet tool install --global dotnet-ef --version 10.0.0
if (!(Test-Path src/Web/appsettings.json)) {
    Copy-Item src/Web/appsettings.json.example src/Web/appsettings.json
}
if (!(Test-Path src/Web/appsettings.Development.json)) {
    Copy-Item src/Web/appsettings.Development.json.example src/Web/appsettings.Development.json
}
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DatabaseInitialization__RunMigrationsOnStartup = 'false'
$env:Seeding__Mode = 'None'
$env:BackgroundJobs__Enabled = 'false'
~~~

Web đã có UserSecretsId, không cần chạy user-secrets init hoặc sửa csproj. JSON template có placeholders: không dùng sample JWT key, SMTP password hoặc OpenAI API key cho môi trường thật. Nếu chưa dùng Email/OpenAI, để trống các field credential tương ứng trong **appsettings local**, tránh gọi dịch vụ với placeholder. Không sửa template của repo để chứa giá trị cá nhân.

Web Development nạp appsettings → appsettings.Development → User Secrets → environment → CLI overrides; Key Vault chỉ thêm nếu AZURE_KEY_VAULT_ENDPOINT được đặt và có thể override cấu hình. Local không dùng Key Vault thì bỏ endpoint đó. Không dùng dotnet user-secrets list trong terminal có logs/screenshots; User Secrets không mã hóa local store.

## 2. Connection string cho SQL của mình

Ví dụ dùng Windows authentication; nhập instance/database của chính bạn, không có server cá nhân hardcode. Không dùng COPY/rehearsal hoặc DB người khác:

~~~powershell
Add-Type -AssemblyName System.Data
$w7Server = Read-Host 'SQL Server LOCAL instance cua ban'
$w7Database = Read-Host 'Ten database LOCAL cua ban'
if ([string]::IsNullOrWhiteSpace($w7Server) -or [string]::IsNullOrWhiteSpace($w7Database)) { throw 'Thieu target.' }
$w7Sql = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$w7Sql['Data Source'] = $w7Server
$w7Sql['Initial Catalog'] = $w7Database
$w7Sql['Integrated Security'] = $true
$w7Sql['Encrypt'] = $true
$w7Sql['TrustServerCertificate'] = $true # chỉ cho SQL local với certificate chưa trusted
$env:ConnectionStrings__MyCnn = $w7Sql.ConnectionString
# Lưu cho Web lần sau; stdin JSON, không in connection string:
@{ 'ConnectionStrings:MyCnn' = $env:ConnectionStrings__MyCnn } |
    ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc connection.' }
~~~

Giữ environment này cho EF lẫn Web CLI; terminal mới phải thiết lập lại cho EF. EF factory không tự nạp User Secrets và cần chạy từ src/Infrastructure. Nếu dùng SQL authentication, nhận connection qua secret tooling hoặc nhập bằng Read-Host -AsSecureString, không viết mật khẩu trong lệnh/Markdown. Xác minh server/catalog bằng SSMS trước bất kỳ thao tác ghi.

## 3. EncryptionKey và Jwt:Key — không ghi giá trị vào terminal logs

**DB đã có GCM hoặc restore backup GCM:** nhận đúng EncryptionKey qua password manager của người giữ key. Không tạo key mới. Nhập ẩn và truyền JSON qua stdin:

~~~powershell
$w7Input = Read-Host 'EncryptionKey hien huu cua dataset (Base64 32 bytes)' -AsSecureString
$w7Key = [System.Net.NetworkCredential]::new('', $w7Input).Password
try {
    if ([Convert]::FromBase64String($w7Key).Length -ne 32) { throw 'Key phai du 32 bytes.' }
    @{ 'Security:EncryptionKey' = $w7Key } | ConvertTo-Json -Compress |
        dotnet user-secrets set --project src/Web
    if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc key.' }
} finally { $w7Input.Dispose(); Remove-Variable w7Key -ErrorAction SilentlyContinue }
~~~

**Chỉ DB mới/rỗng, hoặc dataset chỉ plaintext chưa có GCM**, được tạo key riêng. Script dưới từ chối nếu Web User Secrets đã có EncryptionKey để tránh đổi nhầm key của DB khác; các checkout dùng cùng UserSecretsId có thể dùng chung store trên cùng máy/user:

~~~powershell
$w7Project = [xml](Get-Content src/Web/Web.csproj -Raw)
$w7SecretId = @($w7Project.Project.PropertyGroup.UserSecretsId | Where-Object { $_ })[0]
$w7SecretFile = Join-Path $env:APPDATA "Microsoft/UserSecrets/$w7SecretId/secrets.json"
$w7Existing = if (Test-Path $w7SecretFile) { Get-Content $w7SecretFile -Raw | ConvertFrom-Json } else { @{} }
if ($w7Existing.'Security:EncryptionKey' -or $w7Existing.Security.EncryptionKey) {
    throw 'Da co key: giu nguyen, kiem tra dataset; khong tao de len.'
}
function New-W7RandomKey {
    $w7Bytes = New-Object byte[] 32
    $w7Rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $w7Rng.GetBytes($w7Bytes); [Convert]::ToBase64String($w7Bytes) }
    finally { $w7Rng.Dispose(); [Array]::Clear($w7Bytes, 0, $w7Bytes.Length) }
}
$w7NewSecrets = @{ 'Security:EncryptionKey' = (New-W7RandomKey) }
if (-not ($w7Existing.'Jwt:Key' -or $w7Existing.Jwt.Key)) { $w7NewSecrets['Jwt:Key'] = New-W7RandomKey }
$w7NewSecrets | ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc secrets.' }
Remove-Variable w7NewSecrets,w7Existing
~~~

Jwt:Key là JWT signing secret khác EncryptionKey. Máy đã nhập EncryptionKey cũ nhưng chưa có JWT key thì có thể dùng password manager tạo secret ngẫu nhiên mạnh và nhập ẩn theo block đầu, đổi tên key trong hashtable thành Jwt:Key; hoặc dùng RNG32 bytes như trên để tạo riêng. Đừng dùng sample từ template. Giữ Jwt:Issuer/Jwt:Audience khớp giữa token generation và validation; đổi signing key làm token cũ không còn hợp lệ.

Chỉ khi máy chưa có JWT key, nhập secret ngẫu nhiên mạnh từ password manager bằng block sau (bỏ qua nếu script DB mới đã tạo Jwt:Key). Không thay signing key hiện hữu ngoài ý muốn:

~~~powershell
$w7JwtInput = Read-Host 'JWT signing secret rieng (khong phai EncryptionKey)' -AsSecureString
$w7Jwt = [System.Net.NetworkCredential]::new('', $w7JwtInput).Password
try {
    if ([System.Text.Encoding]::UTF8.GetByteCount($w7Jwt) -lt 32) { throw 'JWT secret qua ngan.' }
    @{ 'Jwt:Key' = $w7Jwt } | ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
    if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc JWT key.' }
} finally { $w7JwtInput.Dispose(); Remove-Variable w7Jwt -ErrorAction SilentlyContinue }
~~~
Security:LegacyEncryptionKey chỉ cần nếu dry-run chuyển sensitive logs AES-256-CBC cũ. Nhận đúng chuỗi legacy secret qua password manager; không áp dụng kiểm tra Base64/32 bytes của GCM key và không tự tạo legacy key mới để thử decrypt. Dùng block sau chỉ khi thực sự có CBC logs:


~~~powershell
$w7LegacyInput = Read-Host 'Legacy CBC secret nguyen goc' -AsSecureString
try {
    @{ 'Security:LegacyEncryptionKey' = [System.Net.NetworkCredential]::new('', $w7LegacyInput).Password } |
        ConvertTo-Json -Compress | dotnet user-secrets set --project src/Web
    if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc legacy key.' }
} finally { $w7LegacyInput.Dispose() }
~~~
Backup EncryptionKey: mở Web Manage User Secrets trên máy riêng, lưu giá trị vào KeePassXC/password manager, gắn với **server/database/dataset**, lưu bản sao vault mã hóa độc lập và thử mở lại. Không đưa key/vault/.bak vào Git. Backup GCM chỉ đọc được khi còn đúng key; không có automatic rotation hoặc recovery nếu mất key. Không tự backup key bằng plaintext text file.

## 4. Startup safety và kiểm tra trước chạy API

| Key | Giá trị thường dùng | Khi đổi |
|---|---|---|
| DatabaseInitialization:RunMigrationsOnStartup | false | true cho phép startup ghi schema; ưu tiên EF CLI được review. |
| Seeding:Mode | None | System thêm role thiếu, không đổi quyền/mô tả cũ hoặc tạo account. Demo ghi data mẫu, chỉ Development + DB mới/rỗng; có data thì skip, lỗi rollback, restart không seed lại. |
| BackgroundJobs:Enabled | false | true bật5 hosted writer jobs; chỉ bật khi cần thử và chấp nhận thay đổi nghiệp vụ. Demo không tự bật jobs. |

Config không hợp lệ fail trước startup writes. DisputesSeeder không xóa disputes/messages hoặc patch wallets. Các API và request-triggered tasks vẫn có thể ghi khi người dùng gọi chức năng.

Muốn thêm roles thiếu vào local DB, đây là **GHI DATA**, sau kiểm tra schema/key/target:

~~~powershell
$env:Seeding__Mode = 'System'
dotnet run --project src/Web --no-launch-profile
# Sau Ctrl+C:
$env:Seeding__Mode = 'None'
~~~

Demo commands và quy trình DB mới/cũ ở [migration guide](W7-01-README.md). Trước normal startup, phải hoàn thành schema và CLI dry-run0/0/0 ở đó; nếu schema còn thiếu thì migration trước, không thử F5 để tự sửa. Không có default account ở Mode=None/System; DB rỗng muốn đăng nhập bằng data mẫu phải chủ động chọn Demo trên dataset đủ điều kiện.

Sau checks, chạy API từ root:

~~~powershell
dotnet dev-certs https --trust
$env:ASPNETCORE_URLS = Read-Host 'URL HTTPS local cho API, gom cong con trong'
dotnet run --project src/Web --no-launch-profile
~~~

Đọc URL thực tế từ console; không lấy port của máy người khác. Đảm bảo launch profile/environment/VS không override target/key/flags. --no-launch-profile bỏ launchSettings, không tự tắt writer flags đã opt-in.

## 5. Tùy chọn backend

- Email: EmailSettings:SmtpServer, Port, SenderEmail, SenderPassword. Để trống sender credentials khi không dùng; code skip gửi mail. Không dùng địa chỉ/mật khẩu cá nhân trong template.
- Redis: Redis:ConnectionString để SignalR backplane; có thể để trống cho local một instance. Nếu bật, server phải kết nối được; credentials qua secrets.
- OpenAI: OpenAI:ApiKey, BaseUrl, Model; để ApiKey rỗng nếu không dùng AI moderation. Không copy API key vào frontend hoặc logs.

## 6. Frontend

Terminal riêng; không chạy frontend trước khi backend đúng DB/key/schema. Từ root:

~~~powershell
Push-Location src/Web/ClientApp
npm.cmd ci
# Nhập URL backend thực tế, gồm scheme/port, không chứa credentials:
$w7ApiUrl = Read-Host 'URL HTTPS cua backend'
$env:ASPNETCORE_URLS = $w7ApiUrl
$env:REACT_APP_HUB_BASE_URL = $w7ApiUrl
# setupProxy ưu tiên HTTPS_PORT trước URLS: bỏ override cũ nếu không dùng:
Remove-Item Env:ASPNETCORE_HTTPS_PORT -ErrorAction SilentlyContinue
npm.cmd start
# Ctrl+C xong:
Pop-Location
~~~

setupProxy.js proxy /api, /Identity và /hubs, có ws:true. REACT_APP_HUB_BASE_URL dùng cho dispute/notification hubs; restart React sau khi đổi environment. npm prestart chuẩn bị HTTPS dev certificate; có thể cần chạy dotnet dev-certs https --trust trước. Nếu dùng backend origin trực tiếp, kiểm tra auth/CORS/HTTPS tương ứng, không dùng wildcard bỏ authorization để chữa lỗi.

W7-01 thay SensitiveLogsPage/sensitiveLogService: chỉ hiển thị decrypt result integrityVerified, clear modal/new request, log lỗi không chứa response payload. Financial lists trả masked/protected values. Frontend chỉ dùng JWT access token của phiên đăng nhập, **không nhận JWT signing secret**, EncryptionKey hoặc SQL credentials.

## 7. Troubleshooting

| Lỗi | Kiểm tra/cách xử lý |
|---|---|
| Thiếu/invalid EncryptionKey | User Secrets đúng Web UserSecretsId; Base64 giải ra32 bytes; Development provider và environment override. Không in key để debug. |
| Decrypt/integrity fail | Giữ DB/key nguyên trạng; đối chiếu key của backup/dataset, legacy key nếu CBC, dữ liệu bị hỏng. Không tạo key mới hoặc fallback plaintext. |
| SQL login/network/wrong database | Instance/service SQL, authentication, catalog và environment của terminal EF/Web. Factory không đọc User Secrets. |
| Pending EF migration/missing column/table | Theo migration guide, backup và review history trước EF update; không EnsureCreated/drop/history giả. |
| Plaintext còn lại/startup chặn conversion | Sau schema đúng, CLI dry-run; chỉ apply khi backup/key/target/writers PASS; post0/0/0. |
| API không startup | Xem lỗi không chứa secrets: key/config mode, Demo ngoài Development, schema/conversion chưa sẵn sàng, HTTPS/port bị chiếm. Đừng bật migration/seed/jobs để thử chữa. |
| Frontend API/hub lỗi | Backend đang chạy, URL/proxy/REACT_APP_HUB_BASE_URL khớp, certificate trusted, restart React, auth token/roles phù hợp. 403 decrypt với role khác SuperAdmin là đúng. |

Regression commands: dotnet test tests/Application.UnitTests, tests/Domain.UnitTests; functional tests cần Docker và dùng container riêng. UI: trong ClientApp chạy npm.cmd test -- --watchAll=false --runTestsByPath src/pages/SensitiveLogsPage.test.jsx. Không dùng DB cá nhân làm test target, không bật transcript hoặc chụp secret/PII khi kiểm tra.
