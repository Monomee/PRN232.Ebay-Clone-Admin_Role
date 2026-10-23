# Chiến lược migration tương thích ngược (expand/contract)

Pod API cũ và pod API mới dùng chung một SQL Server trong rolling update. `ApplicationDbContextInitialiser` gọi `MigrateAsync()` trước khi process nghe cổng 8080, nên pod mới có thể đổi schema trong lúc bản trước vẫn đang phục vụ.

Migration của bản đang deploy phải để bản trước chạy tiếp. Mọi thay đổi schema làm gãy bản cũ phải tách thành hai lần phát hành.

Cách rollout và script demo nằm ở [RollingUpdate.md](RollingUpdate.md).

## Ba bước

1. **Expand.** Migration chỉ thêm thứ mới: cột nullable, cột có giá trị mặc định, bảng mới, hoặc index mới. Chưa xóa cột, chưa đổi tên, chưa đổi kiểu, chưa thêm `NOT NULL` khi không có default. Pod đang chạy không cần biết đối tượng mới.

2. **Deploy code hiểu cả hai hình.** Code mới ghi cả cột cũ lẫn cột mới, và đọc được cả hai. Code cũ chỉ dùng cột cũ. Hai bản cùng sống đến khi rollout xong. `/health/ready` gọi `CanConnectAsync()`. Pod chỉ vào Service sau khi kết nối đó thành công.

3. **Contract, ở một bản sau.** Khi mọi pod đã chạy code mới, migration tiếp theo mới xóa cột cũ hoặc siết `NOT NULL`. Không gộp expand và contract vào cùng một lần deploy.

## Ví dụ

Đổi `Orders.Status` (chuỗi) sang khóa ngoại `StatusId`:

| Bản | Migration | Code |
|---|---|---|
| Bản này | Thêm `StatusId` nullable. Giữ `Status`. | Pod mới ghi cả hai cột và đọc được cả hai. Pod cũ vẫn chỉ đọc và ghi `Status`. |
| Bản sau | Điền `StatusId` cho dữ liệu cũ, rồi xóa `Status`. | Mọi pod đã ghi `StatusId`. Không còn process nào đọc `Status`. |

## Repo này làm gì lúc khởi động

- Mỗi pod mới chạy `MigrateAsync()`, rồi seeder, rồi mới tới `app.Run()`.
- Hai pod mới có thể lên cùng lúc. EF ghi migration đã áp vào `__EFMigrationsHistory` và khóa bảng đó. Pod đến sau chờ, rồi thấy migration đã xong.
- Migration vừa commit thì pod cũ thấy schema mới ngay. Vì vậy migration không được xóa thứ pod cũ còn dùng.
- Migration ném exception thì process không nghe cổng, readiness thất bại. `maxUnavailable: 0` giữ pod cũ tiếp tục phục vụ.
- `kubectl rollout undo` chỉ trả image về bản trước. Schema đã áp không bị hoàn tác. Pod v1 sau undo vẫn phải đọc ghi được schema mà v2 đã đổi.

## Thay đổi làm gãy rollout

- Xóa hoặc đổi tên cột mà code đang chạy trên production còn đọc hoặc ghi.
- Đặt `NOT NULL` trong khi pod cũ vẫn insert mà không gửi giá trị cho cột đó.
- Đổi kiểu cột ngay trên cột cũ.
- Để migration contract trong cùng image với code đã ngừng ghi cột cũ.
