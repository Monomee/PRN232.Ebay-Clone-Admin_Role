# ??? Database Configuration Guide

## Cách 1: S? d?ng User Secrets (Khuy?n ngh? cho Development)

User Secrets cho phép m?i developer lýu connection string riêng **không b? commit vào Git**.

### Các bý?c th?c hi?n:

#### 1. M? terminal t?i thý m?c Web project:
```powershell
cd src/Web
```

#### 2. Kh?i t?o User Secrets:
```powershell
dotnet user-secrets init
```

#### 3. Thêm connection string c?a b?n:

**Cho LocalDB (M?c ð?nh):**
```powershell
dotnet user-secrets set "ConnectionStrings:EbayCloneDb" "Server=(localdb)\mssqllocaldb;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True"
```

**Cho SQL Server v?i Windows Authentication:**
```powershell
dotnet user-secrets set "ConnectionStrings:EbayCloneDb" "Server=YOUR_SERVER_NAME;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True"
```

**Cho SQL Server v?i SQL Authentication:**
```powershell
dotnet user-secrets set "ConnectionStrings:EbayCloneDb" "Server=YOUR_SERVER_NAME;uid=sa;password=YOUR_PASSWORD;Database=CloneEbayDB;Encrypt=True;TrustServerCertificate=True"
```

#### 4. Xem connection string ð? lýu:
```powershell
dotnet user-secrets list
```

#### 5. Ch?y project:
```powershell
dotnet run
```

---

## Cách 2: S?a tr?c ti?p appsettings.Development.json (Nhanh nhýng KHÔNG nên commit)

?? **LÝU ?:** File này có th? b? commit vào Git, nên ch? dùng t?m th?i.

1. M? file `src/Web/appsettings.Development.json`
2. S?a connection string:

```json
{
  "ConnectionStrings": {
    "EbayCloneDb": "Server=YOUR_SERVER;uid=sa;password=YOUR_PASS;Database=CloneEbayDB;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

3. Thêm file này vào `.gitignore` ð? tránh commit:

```
# Add to .gitignore
src/Web/appsettings.Development.json
```

---

## Cách 3: S? d?ng Environment Variables (Khuy?n ngh? cho Production)

### Trên Windows:
```powershell
$env:ConnectionStrings__EbayCloneDb="Server=YOUR_SERVER;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/Web
```

### Trên Linux/Mac:
```bash
export ConnectionStrings__EbayCloneDb="Server=YOUR_SERVER;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/Web
```

---

## Cách 4: Dùng launchSettings.json (Visual Studio/Rider)

1. M? file `src/Web/Properties/launchSettings.json`
2. Thêm environment variable trong profile:

```json
{
  "profiles": {
    "https": {
      "environmentVariables": {
        "ConnectionStrings__EbayCloneDb": "Server=YOUR_SERVER;uid=sa;password=YOUR_PASS;Database=CloneEbayDB;Encrypt=True;TrustServerCertificate=True"
      }
    }
  }
}
```

---

## ?? Thông tin Connection String m?u

### LocalDB (Default):
```
Server=(localdb)\mssqllocaldb;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True
```

### SQL Server Express (Windows Auth):
```
Server=.\SQLEXPRESS;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True
```

### SQL Server (SQL Auth):
```
Server=localhost;uid=sa;password=YourPassword123;Database=CloneEbayDB;Encrypt=True;TrustServerCertificate=True
```

### SQL Server trên m?ng:
```
Server=192.168.1.100;uid=sa;password=YourPassword123;Database=CloneEbayDB;Encrypt=True;TrustServerCertificate=True
```

---

## ?? First Run - T?o Database l?n ð?u

Sau khi c?u h?nh connection string, ch?y:

```powershell
# T?o database và apply migrations
cd src/Infrastructure
dotnet ef database update --context ApplicationDbContext --startup-project ../Web

# Ho?c ch?y app (s? t? ð?ng migrate)
cd ../Web
dotnet run
```

---

## ?? B?O M?T

- ? **User Secrets:** Connection string ðý?c lýu ? `%APPDATA%\Microsoft\UserSecrets\`, không b? commit
- ? **Environment Variables:** Không lýu trong code
- ? **appsettings.json:** KHÔNG BAO GI? lýu password trong file này n?u project là public

---

## ?? KHUY?N NGH?

**Cho team development:**
1. Dùng User Secrets
2. M?i ngý?i t? config connection string c?a m?nh
3. Commit file `appsettings.Development.json` v?i connection string m?u (không có password th?t)

**Cho production:**
1. Dùng Environment Variables ho?c Azure Key Vault
2. Không hardcode password trong code
