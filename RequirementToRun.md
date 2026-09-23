
### PHẦN 1: CÁC CÔNG CỤ CẦN CÓ (PREREQUISITES)

1. **.NET SDK:** 
   - Dự án được cấu hình chạy trên **.NET 10** (`net10.0` qua `global.json`).
   - *(Máy của bạn đã cài đặt sẵn `.NET SDK 10.0.401`).*
2. **Node.js & npm:** 
   - Cần Node.js (v18 trở lên) để chạy Frontend React (`ClientApp`).
   - *(Máy của bạn đã có Node `v24.15.0` và npm `11.12.1`).*
3. **Cơ sở dữ liệu (SQL Server):**
   - Cần một instance SQL Server (SQL Server Express, LocalDB, SQL Server 2019/2022 hoặc Docker SQL Server).
4. **Công cụ quản lý DB (Tùy chọn):** SSMS (SQL Server Management Studio) hoặc Azure Data Studio.

---

### PHẦN 2: CẤU HÌNH KẾT NỐI DATABASE (QUAN TRỌNG NHẤT)

Hệ thống sử dụng Entity Framework Core với key kết nối chính xác trong mã nguồn là: **`MyCnn`** (chứ không phải `EbayCloneDb` như một số tài liệu cũ ghi).

Bạn có thể cấu hình connection string theo **1 trong 3 cách**:

#### Cách 1: Sửa trực tiếp file `src/Web/appsettings.json` hoặc `appsettings.Development.json`
Mở file [appsettings.Development.json](file:///c:/Users/hoang/Downloads/Docs/prn232/Project%20code/Group3_v2/ebay_clone_adminRole/src/Web/appsettings.Development.json) và cập nhật chuỗi kết nối phù hợp với máy của bạn:

- **Nếu dùng SQL Server Authentication (sa):**
  ```json
  {
    "ConnectionStrings": {
      "MyCnn": "Server=localhost;Database=CloneEbayDB;User Id=sa;Password=MậtKhẩuCủaBạn;Encrypt=True;TrustServerCertificate=True;"
    }
  }
  ```
- **Nếu dùng Windows Authentication:**
  ```json
  {
    "ConnectionStrings": {
      "MyCnn": "Server=.;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True;"
    }
  }
  ```
- **Nếu dùng LocalDB (đi kèm Visual Studio):**
  ```json
  {
    "ConnectionStrings": {
      "MyCnn": "Server=(localdb)\\mssqllocaldb;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True;"
    }
  }
  ```

#### Cách 2: Dùng .NET User Secrets (Khuyên dùng - không lo bị push password lên Git)
Mở terminal tại thư mục `src/Web` và chạy:
```powershell
cd src/Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:MyCnn" "Server=.;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

---

### PHẦN 3: CÁC CẤU HÌNH PHỤ TRỢ KHÁC TRONG `appsettings.json`

File [appsettings.json](file:///c:/Users/hoang/Downloads/Docs/prn232/Project%20code/Group3_v2/ebay_clone_adminRole/src/Web/appsettings.json) đã được chuẩn bị sẵn các giá trị mặc định, tuy nhiên bạn cần chú ý các mục sau:

1. **`InternalIps` (Bảo mật IP cho Admin):**
   - Hệ thống có [InternalIpMiddleware](file:///c:/Users/hoang/Downloads/Docs/prn232/Project%20code/Group3_v2/ebay_clone_adminRole/src/Web/Infrastructure/InternalIpMiddleware.cs) chặn truy cập lạ. Mặc định đã cho phép `127.0.0.1`, `::1`, `192.168.1.*`, `10.*`.
   - Nếu bạn test qua mạng LAN khác hoặc IP khác mà bị lỗi `403 Forbidden`, hãy thêm dải IP của bạn vào mảng `"InternalIps"`.
2. **`Redis` (SignalR Backplane):**
   - Mặc định `"ConnectionString": ""` (để trống). Hệ thống sẽ tự động fallback sang **SignalR In-Memory**, bạn **không cần cài Redis** khi chạy dưới local.
3. **`Jwt` & `EmailSettings` & `OpenAI`:**
   - Đã được điền sẵn thông số mẫu (mock/demo), phục vụ xác thực token, gửi email thông báo và AI hỗ trợ giải quyết tranh chấp (VeRO/Dispute).

---

### PHẦN 4: KHỞI TẠO VÀ SEED DATABASE

Bạn **KHÔNG CẦN CHẠY SCRIPT SQL THỦ CÔNG**. 
Khi chạy backend, phương thức `app.InitialiseDatabaseAsync()` trong [Program.cs](file:///c:/Users/hoang/Downloads/Docs/prn232/Project%20code/Group3_v2/ebay_clone_adminRole/src/Web/Program.cs) sẽ:
1. Tự động kiểm tra và tạo database `CloneEbayDB`.
2. Tự động chạy toàn bộ 28+ bản EF Core Migrations (tạo đủ 39 bảng).
3. Tự động chạy các Seeder nạp sẵn dữ liệu mẫu đầy đủ.

*(Tùy chọn)*: Nếu bạn muốn chạy migration bằng tay trước để kiểm tra DB trong SSMS:
```powershell
# Cài đặt ef tool nếu chưa có: dotnet tool install --global dotnet-ef
cd src/Infrastructure
dotnet ef database update --context ApplicationDbContext --startup-project ../Web
```

---

### PHẦN 5: CÀI ĐẶT THƯ VIỆN FRONTEND (CLIENTAPP)

Mặc dù trong dự án đã có sẵn `node_modules`, nhưng để đảm bảo không bị thiếu package:
```powershell
cd src/Web/ClientApp
npm install --legacy-peer-deps
```

---

### PHẦN 6: TIẾN HÀNH CHẠY DỰ ÁN (3 CÁCH)

#### 👉 Cách 1: Chạy Fullstack tự động bằng .NET (Đơn giản nhất)
Chỉ cần chạy duy nhất project Web, .NET SpaProxy sẽ tự động khởi động cả Backend và Frontend React:
```powershell
# Tin tưởng chứng chỉ dev HTTPS nếu chưa làm
dotnet dev-certs https --trust

# Khởi chạy dự án
cd src/Web
dotnet run
```
Hệ thống sẽ build và mở trình duyệt với:
- **Backend API:** `https://localhost:5001` (hoặc `http://localhost:5000`)
- **Frontend React:** `https://localhost:44447`

---

#### 👉 Cách 2: Chạy tách biệt Backend & Frontend (Khuyên dùng khi dev/debug)
Mở **2 tab terminal riêng biệt**:

- **Terminal 1 (Backend .NET):**
  ```powershell
  cd src/Web
  dotnet run
  ```
- **Terminal 2 (Frontend React):**
  ```powershell
  cd src/Web/ClientApp
  npm start
  ```

---

#### 👉 Cách 3: Chạy qua Docker Compose (Nếu thích dùng Docker)
Dự án đã có sẵn [docker-compose.yml](file:///c:/Users/hoang/Downloads/Docs/prn232/Project%20code/Group3_v2/ebay_clone_adminRole/docker-compose.yml) dựng sẵn SQL Server 2022, Redis và Web:
```powershell
# Tạo file .env từ .env.example
copy .env.example .env

# Khởi chạy toàn bộ container
docker-compose up -d
```

---

### PHẦN 7: KIỂM TRA & TÀI KHOẢN ĐĂNG NHẬP

Sau khi dự án chạy lên thành công:
1. **Giao diện Web:** Truy cập `https://localhost:44447` hoặc `https://localhost:5001`.
2. **Swagger API UI:** Truy cập `https://localhost:5001/api`.
3. **Health Check:** `https://localhost:5001/health`.

**Tài khoản đăng nhập có sẵn từ Seeder:**
- **Tài khoản Quản trị viên tối cao (SuperAdmin):**
  - **Email:** `superadmin@ebay.local`
  - **Mật khẩu:** `Admin123!`
- **Tài khoản Người bán mẫu (Seller):**
  - **Username / Email:** `tech_seller_pro`
  - **Mật khẩu:** `Password123!`

---

### 💡 TỔNG KẾT CHECKLIST LỖI THƯỜNG GẶP
- **Lỗi `Cannot open database "CloneEbayDB"` hoặc timeout:** Kiểm tra lại chuỗi kết nối `MyCnn` và đảm bảo SQL Server Service đang ở trạng thái Running.
- **Lỗi `Access denied. Unauthorized IP`:** Thêm IP hiện tại của máy bạn vào mảng `InternalIps` trong file [appsettings.json](file:///c:/Users/hoang/Downloads/Docs/prn232/Project%20code/Group3_v2/ebay_clone_adminRole/src/Web/appsettings.json).
- **Lỗi chứng chỉ HTTPS trên trình duyệt:** Chạy lệnh `dotnet dev-certs https --trust` và khởi động lại trình duyệt.