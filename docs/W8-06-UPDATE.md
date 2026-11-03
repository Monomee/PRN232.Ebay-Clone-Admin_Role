# W8-06 — cập nhật database sau đợt fix bug

Áp dụng sau khi pull `main` có PR #10–#15. Các PR này **không thêm migration**. Migration mới nhất vẫn là `20261028152242_ProtectSensitiveData` (W7-01).

Có một lỗi dữ liệu seed cần xử lý (z8rp3fcgj0). Seeder cũ tạo order Delivered của Case 5 (return request "Camera lens cracked") nhưng không cộng `SellerEarnings` vào ví seller, nên trang Pending Funds lệch với `PendingBalance`. Seeder đã được sửa, nhưng DB đã seed trước đó không tự sửa vì seeder bỏ qua khi Case 5 đã tồn tại.

Chọn **một** trong hai cách: A (giữ DB đang dùng) hoặc B (tạo DB mới).

Trước khi bắt đầu, mở PowerShell và thiết lập môi trường theo [W7-01-SETUP](W7-01-SETUP.md): biến môi trường Development, `ConnectionStrings__MyCnn`, EncryptionKey.

~~~powershell
git checkout main
git pull
dotnet build src/Web/Web.csproj
~~~

## A. Giữ database đang dùng

1. Dừng API/Visual Studio debug. Backup DB trong SSMS (Tasks → Back Up → Full).
2. Kiểm tra schema đã đủ migration:

   ~~~sql
   SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;
   ~~~

   Kết quả phải là `20261028152242_ProtectSensitiveData`. Nếu chưa có, làm mục A của [W7-01-README](W7-01-README.md) trước.
3. Chạy [W8-06-wallet-fix.sql](W8-06-wallet-fix.sql) trên **đúng database của bạn**. Trong SSMS: File → Open → chọn file, chọn database ở thanh công cụ, rồi bấm Execute. Hoặc dùng sqlcmd:

   ~~~powershell
   sqlcmd -S <SQL instance của bạn> -E -d <tên database của bạn> -b -f 65001 -i docs/W8-06-wallet-fix.sql
   ~~~

   Script tự tìm order Case 5 và seller của nó (không dùng ID cứng), rồi chạy trong một transaction:
   - cộng `SellerEarnings` vào `PendingBalance` và `TotalEarnings` của ví;
   - đặt ngày settle (+21 ngày từ lúc giao) và hạn dispute (+14 ngày);
   - thêm dòng `FeeDeduction` (phí sàn).

   Chạy lại nhiều lần cũng không cộng trùng: lần sau sẽ in "đã được cộng vào ví trước đó".
4. Kiểm tra: mở `/sellers`, bấm Pending của fashion_boutique. Total Pending phải bằng `PendingBalance` của ví.

Lưu ý: nếu ngày settle của order đã qua, lần chạy Settle tiếp theo sẽ chuyển tiền sang AvailableBalance và order biến mất khỏi Pending Funds. Đó là hành vi đúng.

## B. Tạo database mới

Làm theo mục B của [W7-01-README](W7-01-README.md), tóm tắt như sau:

1. Đặt `ConnectionStrings__MyCnn` trỏ tới một **tên database chưa tồn tại**. Tạo key riêng cho DB mới theo setup.
2. Tạo schema bằng EF (DB mới tự được tạo):

   ~~~powershell
   Push-Location src/Infrastructure
   try {
       dotnet ef database update --project . --startup-project ../Web
       if ($LASTEXITCODE -ne 0) { throw 'Schema loi: dung lai.' }
   } finally { Pop-Location }
   ~~~

3. Dry-run dữ liệu nhạy cảm, kỳ vọng `0/0/0`:

   ~~~powershell
   dotnet run --project src/Web --no-launch-profile -- --migrate-sensitive-data
   ~~~

4. Seed dữ liệu mẫu:

   ~~~powershell
   $env:Seeding__Mode = 'Demo'
   $env:BackgroundJobs__Enabled = 'false'
   dotnet run --project src/Web --no-launch-profile
   # Khi thấy "Database seeding completed: Demo", nhấn Ctrl+C, rồi trả về chế độ bình thường:
   $env:Seeding__Mode = 'None'
   ~~~

Seeder mới đã cộng tiền đúng: ví fashion_boutique có `PendingBalance = 1306.50`, bằng Total Pending. **Không chạy** `W8-06-wallet-fix.sql` cho DB mới; nếu lỡ chạy, script cũng chỉ báo "đã được cộng" và không làm gì.

Nếu bỏ qua bước 2, app sẽ dừng ngay khi khởi động với thông báo "Database schema is missing N migration(s)…", kèm lệnh cần chạy.
