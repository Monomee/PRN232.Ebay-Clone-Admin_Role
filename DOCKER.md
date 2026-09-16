# 🐳 Hướng dẫn chạy dự án với Docker

Tài liệu này dành cho **đồng đội** muốn chạy dự án eBay Clone bằng Docker mà **không cần cài .NET SDK, Node.js hay SQL Server** trên máy.

---

## ✅ Yêu cầu

| Công cụ | Version | Link |
|---|---|---|
| Docker Desktop | >= 4.x | https://www.docker.com/products/docker-desktop |
| Git | Bất kỳ | https://git-scm.com |

> **Không cần** cài .NET, Node.js, SQL Server — Docker lo hết!

---

## 🚀 Chạy dự án (Step-by-step)

### Bước 1 — Clone repo

```bash
git clone https://github.com/<your-org>/ebay_clone_adminRole.git
cd ebay_clone_adminRole
```

### Bước 2 — Tạo file `.env`

```bash
# Windows (Command Prompt)
copy .env.example .env

# Windows (PowerShell)
Copy-Item .env.example .env

# macOS / Linux
cp .env.example .env
```

Mở file `.env` vừa tạo và **điền thông tin của bạn**:

```env
SA_PASSWORD=YourStrongPassword123!     # >= 8 ký tự, chữ hoa, số, ký tự đặc biệt
JWT_KEY=YourSuperSecretKeyAtLeast32!   # chuỗi bí mật, tự đặt
EMAIL_SENDER=your@gmail.com            # email gửi thông báo
EMAIL_PASSWORD=xxxx xxxx xxxx xxxx    # Gmail App Password (không phải mật khẩu Gmail)
```

> ⚠️ **Lưu ý**: File `.env` đã được gitignore — bạn sẽ không vô tình push lên GitHub.

### Bước 3 — Build & chạy

```bash
docker-compose up --build
```

Lần đầu sẽ mất **5-10 phút** (tải images, build React, restore NuGet). Các lần sau rất nhanh.

### Bước 4 — Truy cập ứng dụng

| URL | Mô tả |
|---|---|
| http://localhost:5000 | React Frontend (eBay Clone UI) |
| http://localhost:5000/api | Swagger API Docs |
| http://localhost:5000/health | Health check |
| `localhost:1433` | SQL Server (dùng SSMS / DBeaver kết nối) |

---

## 🛑 Dừng dự án

```bash
# Dừng nhưng giữ data
docker-compose down

# Dừng VÀ xóa sạch database (reset hoàn toàn)
docker-compose down -v
```

---

## 🔄 Cập nhật code mới

Khi đồng đội push code mới lên GitHub:

```bash
git pull
docker-compose up --build
```

Flag `--build` bắt Docker build lại image với code mới.

---

## 🐞 Xem logs / Debug

```bash
# Xem log của cả 2 service
docker-compose logs -f

# Chỉ xem log của API
docker-compose logs -f api

# Chỉ xem log của SQL Server
docker-compose logs -f db
```

---

## ❓ Câu hỏi thường gặp

**Q: Nhận lỗi `port is already allocated`?**  
A: Có app khác đang dùng port 5000 hoặc 1433. Mở `docker-compose.yml`, đổi port (ví dụ `"5001:8080"`).

**Q: SA_PASSWORD không hợp lệ?**  
A: SQL Server yêu cầu password phức tạp: >= 8 ký tự, có chữ hoa, chữ thường, số, ký tự đặc biệt (ví dụ: `MyPass123!`).

**Q: API báo không kết nối được DB?**  
A: SQL Server cần thời gian khởi động (~30s). Đợi một lúc rồi thử lại, hoặc chạy `docker-compose restart api`.

**Q: Muốn dùng SQL Server Management Studio (SSMS)?**  
A: Kết nối với: Server = `localhost,1433`, Login = `sa`, Password = giá trị `SA_PASSWORD` trong file `.env` của bạn.
