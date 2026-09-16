<div align="center">
  <h1> eBay Clone - Admin & Backend API</h1>
  <i>Hệ thống backend và nền tảng quản trị (Admin) cho dự án eBay Clone.</i>
</div>

---

## 1. Giới thiệu project
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
│   └── Web/                 # Razor Pages & RESTful APIs (Endpoints) & React Frontend (ClientApp)
├── tests/                   # Unit Tests, Functional Tests, Integration Tests
└── artifacts/               # Đầu ra build (được cấu hình qua Directory.Packages.props)
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
  - Phân quyền theo Role cho đội ngũ quản trị (`SuperAdmin`, `Support`, `Monitor`).
  - Ghi nhận lịch sử thao tác hệ thống toàn diện (`AuditLogs`).
  - Xác thực 2 lớp (2FA) cho các tài khoản quản trị.
- **Quản lý Người dùng:**
  - Danh sách người dùng, xem chi tiết và lịch sử hoạt động.
  - Khóa/Mở khóa tài khoản, phê duyệt người dùng (`ApproveUser`).
- **Quản lý Sản phẩm & Danh mục:**
  - Phân loại danh mục hệ thống đa cấp (`Categories`).
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
- **Framework:** .NET 10.0 (Preview) / .NET 8.0
- **Frontend:** React 18 (Bootstrap 5, Chart.js, Recharts, SignalR Client)
- **Architecture:** Clean Architecture, CQRS (MediatR), Repository Pattern
- **Cơ sở dữ liệu:** SQL Server (EF Core Code-First)
- **Authentication:** ASP.NET Core Identity & JWT Tokens
- **Real-time:** SignalR
- **Validation:** FluentValidation
- **Testing:** xUnit, Moq, Shouldly, NUnit
- **Deployment:** Docker, Docker Compose, Kubernetes
- **UI (Admin):** Razor Pages, Reactjs
---

## 5. Cách chạy project (Step by Step)

### 5.1. Yêu cầu hệ thống
- .NET SDK (v8.0 hoặc v10.0).
- Node.js (v16 trở lên) để chạy Frontend.
- SQL Server (hoặc SQL Server Express / LocalDB).
- Docker (Tùy chọn nếu muốn chạy qua môi trường container).

### 5.2. Chạy môi trường Local (Windows / MacOS / Linux)

**Bước 1: Thiết lập cơ sở dữ liệu**
*Có thể chạy script thiết lập tự động (nếu sử dụng Windows):*
```powershell
.\setup-database.ps1
```
*Hoặc cấu hình qua User Secrets:*
```bash
cd src/Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:EbayCloneDb" "Server=(localdb)\mssqllocaldb;Database=CloneEbayDB;Trusted_Connection=True;TrustServerCertificate=True"
```
*(Hoặc sửa trực tiếp file `appsettings.Development.json` dựa trên `appsettings.Development.json.example`).*

**Bước 2: Migrate Database**
```bash
cd src/Infrastructure
dotnet ef database update --context ApplicationDbContext --startup-project ../Web
```

**Bước 3: Chạy ứng dụng**
```bash
cd src/Web
dotnet run
```
Trang web sẽ tự động mở hoặc bạn có thể truy cập `https://localhost:5001`. Cơ sở dữ liệu sẽ được tự động seed dữ liệu mẫu trong lần chạy đầu tiên.

---

## 6. Tài khoản Demo
Sau khi hệ thống khởi tạo và chạy tự động Seeder, bạn có thể đăng nhập bằng các tài khoản sau:

* Admin Account:**
- **Email:** `superadmin@ebay.local`
- **Password:** `Admin123!`

**Sample Seller Account:**
- **Username:** `tech_seller_pro`
- **Password:** `Password123!`

---

## 7. API / Module chính
Hệ thống tổ chức source code trong `src/Application` theo các Use Cases ứng với mỗi Feature:
- `AdminRoles` / `Users`: Lõi xử lý thông tin tài khoản người dùng và quyền hạn Admin.
- `Products` / `Categories`: Các Use Cases Query/Command dữ liệu sản phẩm.
- `Financials` / `Withdrawals` / `PlatformFees`: Mô-đun xử lý dòng tiền của hệ thống.
- `Reports` / `Stats` / `Dashboard`: Mô-đun lấy dữ liệu phân tích dạng aggregate hoặc real-time.
- `Disputes` / `ReturnRequests`: Mô-đun xử lý khiếu nại và tranh chấp.

*Lưu ý: NSwag hiện đang được disable do cấu hình mặc định tương thích chưa được hỗ trợ, nhưng có thể xem Swagger UI bình thường tại endpoint `/swagger` theo cấu hình `Program.cs`.*

---

## 8. Hướng phát triển thêm
- Tích hợp thêm Payment Gateway thực tế (VNPay, Stripe, PayPal, v.v.)
- Hoàn thiện hệ thống Chat / Messaging real-time giữa Buyer và Seller.
- Bổ sung ElasticSearch cho phần tìm kiếm sản phẩm để tăng hiệu năng.
- Bổ sung Caching (Redis) ở các query thống kê để giảm tải database.
- Tự động hóa CI/CD pipelines trên GitHub Actions cho kịch bản triển khai prod.

---
> *Template dự án gốc thuộc về [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/CleanArchitecture).*