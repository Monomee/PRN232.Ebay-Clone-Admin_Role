# 11. 📉 Phân quyền rõ ràng cho từng loại admin (Monitor, Support...)

## Mô tả trong dự án

### Vai trò có sẵn và quyền (policy)

Khai báo ở `src/Infrastructure/DependencyInjection.cs` (`AddGrantablePolicy`), hằng số ở `Domain/Constants/Policies.cs` và `Roles.cs`.

| Policy | SuperAdmin | Support | Monitor | Administrator | Custom role |
|---|:-:|:-:|:-:|:-:|:-:|
| `ViewDashboard` | ✅ | ✅ | ✅ | | nếu được cấp |
| `ViewReports` (doanh thu, user mới, đơn) | ✅ | | ✅ | | nếu được cấp |
| `ManageUsers` | ✅ | ✅ | | ✅ | nếu được cấp |
| `ManageProducts` | ✅ | ✅ | | ✅ | nếu được cấp |
| `ManageOrders` (đơn, hoàn trả) | ✅ | ✅ | | ✅ | nếu được cấp |
| `ManageDisputes` | ✅ | ✅ | | ✅ | nếu được cấp |
| `ModerateReviews` | ✅ | ✅ | | ✅ | nếu được cấp |
| `ManageBroadcasts` | ✅ | ✅ | | ✅ | nếu được cấp |
| `ManageAdminRoles`, `ViewAuditLogs`, Sensitive Logs, Network Connections, IP blocklist | ✅ | | | | ❌ (chỉ SuperAdmin) |
| Tài chính (phí sàn, payout, ví) | ✅ | | | ✅ | |

Tóm tắt:
- **Monitor:** chỉ *xem* (dashboard, báo cáo). Không sửa được gì.
- **Support:** vận hành hằng ngày (user, sản phẩm, đơn, dispute, review, broadcast). Không đụng tới tài chính hay quyền admin.
- **Administrator:** tài chính.
- **SuperAdmin:** toàn quyền và là người duy nhất quản lý admin khác.

### Custom role (vai trò tự tạo)

- Trang **Admin Roles** (`/admin-roles`, SuperAdmin).
  - Tạo role mới, ví dụ *"QA Lead"*, và tick các quyền (checkbox).
  - Bấm **Edit** ở một role để sửa mô tả và quyền (`PUT /api/AdminRoles/roles/{id}`).
  - Role có sẵn (SuperAdmin, Administrator, Support, Monitor) hiện chữ **Built-in**, không sửa được: quyền của chúng gắn với tên role trong code.
  - Tạo tài khoản admin và gán role cho tài khoản đó.
- Server chỉ nhận **8 quyền cấp được** (`AdminPermissions.Grantable`). Gõ sai tên, hoặc cố cấp quyền chỉ dành cho SuperAdmin như `ManageAdminRoles`, thì bị 400.
- Mỗi lần sửa role ghi `AdminActions` `UpdateAdminRole` (trước/sau) và `SensitiveDataLog` (`AdminPrivilege`).
- Quyền được lưu trong `AdminRole.Permissions` (mảng JSON). Lúc đăng nhập, quyền được đưa vào JWT dưới dạng claim `permission` (`AdminPermissions.cs`).
- Server cấp quyền theo **danh sách quyền được cấp**, **không theo tên role**. Đặt tên role là "SuperAdmin 2" cũng không có thêm quyền.

### Kiểm quyền ở 3 nơi

1. **API (quan trọng nhất):** mỗi nhóm endpoint có `RequireAuthorization(Policies.X)`. Thiếu quyền thì **403**. Đây là lớp thật sự bảo vệ dữ liệu.
2. **Route React** (`AppRoutes.js` → `ProtectedRoute`): mở URL không đủ quyền thì bị chặn. Đây chỉ là trải nghiệm người dùng, không phải bảo mật.
3. **Sidebar** (`NavMenu.js`): chỉ hiện các mục người đó dùng được.
4. **SignalR:** hub `/hubs/dispute` cần `ManageDisputes`, `/hubs/seller` cần `ManageUsers`.

### Đổi quyền có hiệu lực ngay

- `SessionValidation` kiểm ở **mỗi request**.
- Token cũ bị từ chối ngay khi:
  - role của user đổi (gán sang role khác);
  - quyền của custom role bị sửa (nút **Edit**).
- Không phải chờ token hết hạn sau 60 phút.

### Mọi thao tác admin đều có audit

- Trang **Audit Logs** (`/audit-logs`, SuperAdmin): lọc theo admin, đối tượng, hành động, ngày.
- **Người thao tác lấy từ JWT**, không lấy từ body. Gửi `"adminId": 1` trong body để đổ tội cho người khác không có tác dụng.

## Để làm gì

- **Quyền tối thiểu (least privilege):** nhân viên CSKH không cần xem doanh thu, người làm báo cáo không cần khóa user.
- Một tài khoản bị chiếm thì thiệt hại chỉ nằm trong phạm vi quyền của tài khoản đó.
- Custom role giúp công ty tự định nghĩa vị trí mới mà không cần sửa code.

---

## Kịch bản demo

Chuẩn bị 3 cửa sổ: **A** = SuperAdmin, **B** = Support, **C** = Monitor. Cả ba đã bật 2FA.

### Demo 1: mỗi vai trò thấy một sidebar khác nhau (1 phút)

Đặt 3 cửa sổ cạnh nhau và so sidebar:
- SuperAdmin có đủ: Admin Roles, Audit Logs, Sensitive Logs, Network Connections, Payout...
- Support có: Users, Products, Orders, Disputes, Reviews, Broadcasts. Không có Admin Roles, Audit Logs.
- Monitor chỉ có Dashboard, và thấy biểu đồ báo cáo. Support **không** thấy biểu đồ doanh thu (thiếu `ViewReports`).

### Demo 2: gõ thẳng URL hoặc gọi thẳng API cũng không qua (1,5 phút)

1. Ở cửa sổ **C (Monitor)**, gõ `https://localhost:44447/users`: bị chặn hoặc chuyển trang.
2. Câu hỏi: "Ẩn ở giao diện thôi thì hacker gọi thẳng API là xong?" Mở DevTools Console ở cửa sổ C:
   ```js
   (await fetch('/api/Users')).status                  // 403: Monitor không có ManageUsers
   (await fetch('/api/Dashboard/metrics')).status      // 200: Monitor có ViewDashboard
   (await fetch('/api/SystemAdmin/active-ips')).status // 403: chỉ SuperAdmin
   ```
3. Ở cửa sổ **B (Support)**:
   ```js
   (await fetch('/api/Users')).status                  // 200
   (await fetch('/api/Stats/reports/revenue')).status  // 403: Support không xem doanh thu
   (await fetch('/api/AdminRoles/roles')).status       // 403: không quản lý admin được
   (await fetch('/api/SensitiveLogs')).status          // 403
   ```
4. Kết luận: **server tự kiểm**. Giao diện chỉ ẩn nút cho gọn.

### Demo 3: tạo custom role và thấy có hiệu lực (2 phút)

1. Cửa sổ **A**, vào **Admin Roles**, tạo role *"Review Moderator"*. Chỉ tick **View dashboard** và **Reviews & feedback**.
2. Tạo tài khoản admin mới, ví dụ `qa@ebay.local`, gán role vừa tạo.
3. Đăng nhập `qa@ebay.local` ở cửa sổ ẩn danh mới và bật 2FA.
   - Sidebar chỉ có Dashboard và Review Monitoring.
   - `/api/Users` trả 403; `/api/Reviews/flagged` trả 200.
4. **Thu hồi quyền tức thì:**
   - ở cửa sổ A, bấm **Edit** ở role *Review Moderator*, bỏ tick **Reviews & feedback**, bấm **Save Permissions**;
   - ở cửa sổ qa, refresh trang Review Monitoring: **phiên bị từ chối ngay** (401), phải đăng nhập lại;
   - đăng nhập lại thì không còn mục Reviews.
   - Cách khác: gán `qa@ebay.local` sang một role khác (**Modify Access**) cũng cho kết quả như vậy.
5. Ý nghĩa: nhân viên nghỉ việc hoặc bị đổi vị trí thì mất quyền ngay, không chờ token hết hạn.

### Demo 4: audit log ghi đúng người làm (1 phút)

1. Ở cửa sổ **B (Support)**, ban rồi unban một user.
2. Ở cửa sổ **A**, vào **Audit Logs**, lọc theo admin `support`. Thấy 2 dòng `BanUser` và `UnbanUser`, kèm thời gian và đối tượng.
3. Mở **Admin Roles**, phần **Governance Logs**: thấy lịch sử tạo role, sửa role (`UpdateAdminRole`, có quyền trước và sau) và gán role vừa làm ở Demo 3.

### Demo 5: seller hoặc buyer không vào được kênh của admin (30 giây, tùy chọn)

Chạy test:

```bash
dotnet test tests/Application.FunctionalTests --filter "AdminHubs_RefuseSellers"
```

Trước bản vá, mọi user đăng nhập (kể cả seller) đều nhận broadcast dispute của admin qua SignalR. Giờ bị 403.

---

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Custom role có tự cấp được quyền SuperAdmin không? | Không. `ManageAdminRoles`, `ViewAuditLogs` và các trang hệ thống kiểm bằng **role SuperAdmin**. Server còn từ chối luôn việc lưu các quyền này vào role (400) |
| Đổi quyền thì có phải chờ token hết hạn không? | Không, xem Demo 3, bước 4 |
| Sao Administrator không vào được `/api/Users` trong JMeter? | Đúng thiết kế ban đầu (phân tách tài chính và vận hành). Ghi chú trong `tests/jmeter/README.md` |

## Dự phòng

```bash
dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~RbacHttpTests|ChangedPermissions_InvalidateExistingToken|CustomRole_IsAuthorizedByGrantedPermissionsOnly"
dotnet test tests/Application.UnitTests --filter "FullyQualifiedName~UpdateAdminRoleTests"
# JMeter: tests/jmeter/plans/12-security-rbac.jmx
```
