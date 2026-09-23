# 🛠️ Cẩm Nang Xử Lý Lỗi Khi Clone & Khởi Chạy Dự Án (Troubleshooting Guide)

> Tài liệu này tổng hợp toàn bộ các lỗi/vấn đề phổ biến nhất mà lập trình viên thường gặp phải khi clone source code dự án **eBay Clone - Admin & Backend API** về máy cá nhân và hướng dẫn cách khắc phục từng bước chi tiết.

---

## 📑 Danh mục các lỗi thường gặp

| STT | Mã / Dấu hiệu nhận biết lỗi | Thành phần liên quan | Mức độ phổ biến |
| :---: | :--- | :--- | :---: |
| [1](#1-lỗi-a-project-with-an-output-type-of-class-library-cannot-be-started-directly) | `A project with an Output Type of Class Library cannot be started directly` | Visual Studio Startup Project | Rất cao |
| [2](#2-lỗi-kết-nối-sql-server-sqlexception-error-40---could-not-open-a-connection) | `SqlException: (provider: Named Pipes Provider, error: 40 - Could not open a connection)` | SQL Server / Connection String | Rất cao |
| [3](#3-lỗi-không-khởi-động-được-react-spa-development-server-couldnt-start-npm-start) | `Couldn't start the SPA development server with command 'npm start'` / `You specified SSL_CRT_FILE... can't be found` | React ClientApp / SSL Cert | Rất cao |
| [4](#4-lỗi-403-forbidden-access-denied-unauthorized-ip) | Gọi API hoặc mở web bị trả về `403 Forbidden: Access denied. Unauthorized IP` | InternalIpMiddleware | Trung bình |
| [5](#5-lỗi-chứng-chỉ-https-cục-bộ-untrusted-ssl-warning-hoặc-lỗi-bảo-mật-khi-gọi-api) | Trình duyệt báo `Your connection is not private` hoặc SSL không tin cậy | .NET Dev Certs | Trung bình |
| [6](#6-lỗi-thiếu-thư-viện-node_modules-của-react-frontend) | Báo lỗi thiếu package, `react-scripts not recognized` hoặc Web build lỗi | Node.js / NPM | Phổ biến |
| [7](#7-lỗi-liên-quan-đến-redis-connectionstring) | Ứng dụng khởi động chậm hoặc treo khi cố kết nối tới Redis | Redis Backplane | Thấp |
| [8](#8-quên-hoặc-không-biết-tài-khoản-mật-khẩu-đăng-nhập-hệ-thống) | Màn hình đăng nhập yêu cầu email & password, không biết thông tin tài khoản | Seed Data / Identity | Phổ biến |

---

## 1. Lỗi: "A project with an Output Type of Class Library cannot be started directly"

### 🔴 Triệu chứng:
Khi mở Visual Studio và bấm phím **F5** (hoặc nút Play xanh), một hộp thoại thông báo lỗi xuất hiện:
> *"A project with an Output Type of Class Library cannot be started directly. In order to debug this project, add an executable project to this solution which references the library project. Set the executable project as the startup project."*

### 🔍 Nguyên nhân:
Visual Studio đang tự động chọn nhầm **Startup Project** vào một project thư viện (.dll) như `Application`, `Domain` hoặc `Infrastructure`. Các project này không có file thực thi `Program.cs` nên không thể chạy trực tiếp.

### 🟢 Cách xử lý:
1. Trong cửa sổ **Solution Explorer** bên phải Visual Studio, tìm đến thư mục `src` $\rightarrow$ project **`Web`** (hoặc `EbayClone.Web`).
2. **Click chuột phải vào project `Web`** $\rightarrow$ Chọn dòng: **"Set as Startup Project"**.
3. Bạn sẽ thấy tên project `Web` được **in đậm** lên.
4. Bấm lại **F5** để chạy bình thường.

*(Nếu chạy bằng Terminal/CMD, chỉ cần gõ `dotnet run --project src/Web`).*

---

## 2. Lỗi kết nối SQL Server (SqlException: Error 40 - Could not open a connection)

### 🔴 Triệu chứng:
Ứng dụng bị dừng và quăng ngoại lệ tại hàm `await _context.Database.MigrateAsync()` trong file `ApplicationDbContextInitialiser.cs`:
> `Microsoft.Data.SqlClient.SqlException: A network-related or instance-specific error occurred while establishing a connection to SQL Server. (provider: Named Pipes Provider, error: 40 - Could not open a connection to SQL Server)`
> `System.ComponentModel.Win32Exception: The system cannot find the file specified.`

### 🔍 Nguyên nhân:
1. **Khác biệt Instance Name:** Máy bạn cài SQL Server Express (tên instance là `.\SQLEXPRESS` hoặc `TENTHIETBI\SQLEXPRESS`), nhưng cấu hình lại đang trỏ vào `.` (mặc định cho bản Enterprise/Developer đầy đủ).
2. **Bị ghi đè bởi `appsettings.Development.json`:** Bạn đã sửa chuỗi kết nối trong `appsettings.json`, nhưng trong môi trường Debug, ASP.NET Core **ưu tiên nạp `appsettings.Development.json`** và ghi đè lên cấu hình của `appsettings.json`.
3. **Dịch vụ SQL Server chưa bật:** Service `MSSQLSERVER` hoặc `MSSQL$SQLEXPRESS` đang ở trạng thái `Stopped`.

### 🟢 Cách xử lý:

#### Bước 1: Kiểm tra Service SQL Server có đang chạy không
- Nhấn tổ hợp phím `Windows + R`, gõ `services.msc` $\rightarrow$ Enter.
- Tìm dịch vụ **`SQL Server (SQLEXPRESS)`** hoặc **`SQL Server (MSSQLSERVER)`** $\rightarrow$ Đảm bảo trạng thái là **Running**.

#### Bước 2: Đồng bộ cấu hình Connection String ở CẢ HAI FILE
Mở và sửa mục `ConnectionStrings:MyCnn` ở cả hai file:
- File 1: `src/Web/appsettings.json`
- File 2: `src/Web/appsettings.Development.json`

**Nếu bạn dùng SQL Server Express với Windows Authentication (Khuyên dùng):**
```json
"ConnectionStrings": {
  "MyCnn": "Server=.\\SQLEXPRESS;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

**Nếu bạn dùng SQL Authentication (có tài khoản `sa` và mật khẩu):**
```json
"ConnectionStrings": {
  "MyCnn": "Server=.\\SQLEXPRESS;uid=sa;password=YOUR_PASSWORD;Database=CloneEbayDB;Encrypt=True;TrustServerCertificate=True;"
}
```

*(Lưu ý: Thay `.\\SQLEXPRESS` bằng `.` hoặc `localhost` nếu máy bạn dùng bản SQL Server tiêu chuẩn).*

---

## 3. Lỗi không khởi động được React SPA Development Server ("Couldn't start 'npm start'")

### 🔴 Triệu chứng:
Console hiển thị log lỗi:
> `fail: Microsoft.AspNetCore.SpaProxy.SpaProxyLaunchManager[0] Couldn't start the SPA development server with command 'npm start'.`  
> `info: Microsoft.AspNetCore.SpaProxy.SpaProxyMiddleware[0] SPA proxy is not ready. Returning temporary landing page.`  
> Khi chạy `npm start` tay thì xuất hiện thông báo:  
> `You specified SSL_CRT_FILE in your env, but the file "C:\Users\...\AppData\Roaming\ASP.NET\https\ebayclone.web.pem" can't be found.`

### 🔍 Nguyên nhân:
Trong thư mục `src/Web/ClientApp/`, file `.env.development.local` đã bị lưu đường dẫn tuyệt đối trỏ tới thư mục của người tạo ban đầu (ví dụ: `C:\Users\thang\...`). Khi máy khác (ví dụ: user `hoang`) clone về chạy, file `.pem` này không tồn tại khiến React Dev Server crash ngay khi mở.

### 🟢 Cách xử lý:
Chỉ cần thực hiện 3 bước qua terminal:

```powershell
# 1. Di chuyển vào thư mục ClientApp
cd src/Web/ClientApp

# 2. Xóa file cấu hình môi trường cục bộ bị sai đường dẫn
Remove-Item .env.development.local -Force

# 3. Chạy lại script tạo chứng chỉ SSL cho chính máy tính của bạn
npm run prestart
```

Sau khi chạy xong, file `.env.development.local` mới sẽ được tạo với đường dẫn chính xác trỏ về thư mục `AppData` của máy bạn. Bấm chạy lại dự án sẽ hoạt động bình thường!

---

## 4. Lỗi 403 Forbidden ("Access denied. Unauthorized IP")

### 🔴 Triệu chứng:
Truy cập web hoặc gọi bất kỳ API nào đều bị trả về mã lỗi:
> `HTTP 403 Forbidden`  
> Nội dung: `Access denied. Unauthorized IP.`

### 🔍 Nguyên nhân:
Hệ thống sử dụng middleware bảo mật `InternalIpMiddleware` để chỉ cho phép các IP nội bộ hoặc quản trị viên truy cập. Nếu bạn truy cập qua mạng LAN công ty, Wi-Fi khác dải IP, hoặc qua Docker bridge network mà dải IP của bạn chưa được cấp phép trong cấu hình whitelist thì sẽ bị chặn.

### 🟢 Cách xử lý:
1. Mở file `src/Web/appsettings.json` (và `src/Web/appsettings.Development.json`).
2. Tìm đến mục `InternalIps`:
```json
"InternalIps": [
  "127.0.0.1",
  "::1",
  "192.168.1.*",
  "192.168.*.*",
  "172.*",
  "10.*"
]
```
3. Thêm dải IP mạng hiện tại của bạn vào danh sách (hoặc thêm `"192.168.*.*"` để cho phép toàn bộ mạng nội bộ gia đình/văn phòng).

---

## 5. Lỗi chứng chỉ HTTPS cục bộ (Untrusted SSL / Warning trên trình duyệt)

### 🔴 Triệu chứng:
Trình duyệt hiển thị màn hình đỏ cảnh báo: *"Kết nối của bạn không phải là kết nối riêng tư"* hoặc các yêu cầu API từ React sang Backend bị chặn do `ERR_CERT_AUTHORITY_INVALID`.

### 🔍 Nguyên nhân:
Chứng chỉ bảo mật SSL dành cho môi trường phát triển (.NET Development Certificate) chưa được cài đặt hoặc chưa được thêm vào danh sách tin cậy (Trusted Root Certification Authorities) trên máy tính của bạn.

### 🟢 Cách xử lý:
Mở PowerShell (Run as Administrator) và chạy 2 lệnh sau:

```powershell
# Dọn dẹp chứng chỉ cũ bị lỗi (nếu có)
dotnet dev-certs https --clean

# Tạo mới và tin cậy chứng chỉ HTTPS phát triển
dotnet dev-certs https --trust
```
*Chọn **Yes** khi Windows hiển thị hộp thoại xác nhận tin cậy chứng chỉ.*

---

## 6. Lỗi thiếu thư viện node_modules của React Frontend

### 🔴 Triệu chứng:
Visual Studio báo lỗi khi build:
> `Node.js is required to build and run this project.`  
> hoặc không tìm thấy các module React, `react-scripts: command not found`.

### 🔍 Nguyên nhân:
1. Máy chưa cài đặt Node.js hoặc phiên bản quá cũ.
2. Thư mục `node_modules` trong `src/Web/ClientApp` chưa được cài đặt dependencies sau khi clone từ Git.

### 🟢 Cách xử lý:
1. Đảm bảo đã cài đặt **Node.js phiên bản 18 LTS** hoặc **20 LTS** trở lên từ [nodejs.org](https://nodejs.org/).
2. Mở terminal, chạy lệnh cài đặt thư viện:
```powershell
cd src/Web/ClientApp
npm install
```

---

## 7. Lỗi liên quan đến Redis ConnectionString

### 🔴 Triệu chứng:
Hệ thống khởi động bị chậm vài chục giây hoặc quăng lỗi timeout liên quan đến `StackExchange.Redis`.

### 🔍 Nguyên nhân:
Trong `appsettings.json`, mục `Redis:ConnectionString` được điền địa chỉ Redis nhưng trên máy bạn chưa bật dịch vụ Redis.

### 🟢 Cách xử lý:
- **Nếu chạy đơn máy (Single node local development):**  
  Để trống chuỗi kết nối trong `src/Web/appsettings.json`:
  ```json
  "Redis": {
    "ConnectionString": ""
  }
  ```
  *(Khi để trống, SignalR sẽ tự động chạy chế độ In-Memory mà không cần Redis).*
- **Nếu muốn dùng Redis:**  
  Chạy Redis container qua Docker bằng lệnh:
  ```powershell
  docker run -d -p 6379:6379 --name ebay_redis redis:7-alpine
  ```

---

## 8. Quên hoặc không biết thông tin tài khoản đăng nhập hệ thống

### 🔴 Triệu chứng:
Sau khi ứng dụng khởi chạy thành công, giao diện yêu cầu đăng nhập Admin nhưng không biết tài khoản nào hợp lệ.

### 🔍 Thông tin tài khoản được hệ thống tự động sinh (Seed Data):

Sau lần chạy đầu tiên, hệ thống đã tự động chạy Seeder khởi tạo sẵn các tài khoản sau vào database:

#### 👑 Tài khoản Quản trị tối cao (SuperAdmin):
- **Email:** `superadmin@ebay.local`
- **Mật khẩu:** `Admin123!`
- **Quyền hạn:** Toàn quyền quản trị hệ thống, quản lý tài khoản admin, xem audit logs, phân quyền, cấu hình phí sàn.

#### 🛒 Tài khoản Người bán chuyên nghiệp (Sample Seller):
- **Username / Email:** `tech_seller_pro`
- **Mật khẩu:** `Password123!`
- **Quyền hạn:** Đăng sản phẩm, có sẵn ví tiền mẫu, theo dõi trạng thái đơn hàng và tranh chấp.

---

## 📋 Tóm tắt Checklist 5 bước cho người mới Clone dự án về

```bash
# Bước 1: Restore và build mã nguồn Backend
dotnet restore

# Bước 2: Tạo file cấu hình từ template mẫu
cd src/Web
cp appsettings.json.example appsettings.json
# (Chỉnh sửa chuỗi kết nối SQL Server và các cấu hình cá nhân trong appsettings.json nếu cần)
cd ../..

# Bước 3: Cài đặt dependencies cho Frontend
cd src/Web/ClientApp
npm install
cd ../../..

# Bước 4: Đảm bảo SQL Server đang chạy

# Bước 5: Khởi chạy dự án
dotnet run --project src/Web
```
> 🎉 Mở trình duyệt tại địa chỉ: **`https://localhost:5001`** (hoặc xem tài liệu API tại **`https://localhost:5001/api`**).
