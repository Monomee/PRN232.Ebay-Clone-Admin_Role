# Rà soát project và đối chiếu Requirement.txt

Ngày rà soát: 28/10/2026. Nguồn yêu cầu: [Requirement.txt](../Requirement.txt).

## 1. Phạm vi và cách hiểu kết quả

Rà soát cấu trúc solution, mô hình dữ liệu, endpoint, command/query nghiệp vụ, dịch vụ nền, route/giao diện React, lớp gọi API, kiểm thử, Docker, GitHub Actions, Kubernetes và tài liệu trong repository. Không đọc từng dòng thư viện bên thứ ba, file build, ảnh hoặc code tự sinh; không dùng README làm bằng chứng duy nhất cho một tính năng.

- **Có triển khai:** tìm thấy phần xử lý và đường gọi tương ứng trong source; không có nghĩa đã nghiệm thu toàn bộ luồng trên môi trường thật.
- **Một phần:** đã có nền tảng/chức năng, nhưng còn thiếu bước, sai khác hoặc vấn đề ảnh hưởng yêu cầu.
- **Chưa thấy:** không tìm thấy bằng chứng triển khai trong repository. Công việc trên Jira, cluster hoặc dịch vụ bên ngoài cần minh chứng riêng.
- **Cần nghiệm thu:** tiêu chí trải nghiệm/vận hành chưa thể kết luận chỉ bằng đọc code.

Không tính phần trăm hoàn thành: Requirement.txt không chia trọng số cho từng chức năng và kết quả đọc source không tương đương nghiệm thu.

## 2. Project đang làm gì?

Đây là backend và trang quản trị cho sàn thương mại điện tử kiểu eBay. Trọng tâm hiện tại là vận hành sàn: người dùng, kiểm duyệt sản phẩm/review, tranh chấp, hoàn trả, tài chính seller, báo cáo và thông báo. Có thêm trang chủ/chi tiết sản phẩm để hỗ trợ demo các luồng seller/buyer.

Không đủ căn cứ gọi đây là một bản eBay hoàn chỉnh: entity như Bid, Coupon, Inventory, ShippingInfo, Payment có mặt nhưng không đồng nghĩa có đủ API/giao diện đấu giá, giỏ hàng, checkout, vận chuyển và thanh toán thực tế.

### Kiến trúc và công nghệ

| Thành phần | Vai trò và bằng chứng |
|---|---|
| `src/Domain` | 32 file entity nghiệp vụ: User, Product, OrderTable/OrderItem, Dispute/DisputeMessage, ReturnRequest, Review/ReviewReport, Feedback, SellerWallet, FinancialTransaction, PayoutTransaction, AdminRole/AdminUserRole/AdminAction, Notification… |
| `src/Application` | CQRS qua MediatR, DTO, command/query và FluentValidation; các pipeline authorization, validation, logging, xử lý exception. |
| `src/Infrastructure` | EF Core SQL Server, 28 migration chính, seed dữ liệu, Identity, SMTP, JWT, AI moderation, mock payment gateway. |
| `src/Web` | ASP.NET Core Minimal API, Swagger/NSwag, JWT cookie HttpOnly, SignalR, health check, dịch vụ nền, React SPA. |
| Frontend | React 18, React Router 6, Bootstrap/Reactstrap, Chart.js/Recharts, Axios/fetch; xuất Excel và PDF phía trình duyệt. |
| Runtime | .NET 10 theo `Directory.Build.props`/project; connection string thực tế tên `MyCnn`. |
| Triển khai | Docker đa stage, Compose với SQL Server/Redis, manifest K8s/Nginx, GitHub Actions; có thêm Bicep/Azure workflow cần cấu hình môi trường. |

Luồng chính: **React → `/api/...` → MediatR → command/query → `IApplicationDbContext` → EF Core/SQL Server**. SignalR đẩy sự kiện đến giao diện; Redis backplane được bật khi có connection string.

Lưu ý xác thực: login SPA đọc bảng domain `Users`, xác minh BCrypt rồi phát JWT. Project đồng thời còn ASP.NET Identity với `ApplicationUser`. Hai nguồn tài khoản này chưa được hợp nhất; có chỗ gọi IdentityService bằng ID của domain User.

### Các phân hệ đang có

| Phân hệ | Nội dung hiện có | Vị trí chính |
|---|---|---|
| Đăng nhập/2FA | Login, logout, `/me`, QR TOTP, xác minh setup và xác minh OTP khi login | `src/Web/Endpoints/AuthEndpoints.cs`, `src/Application/Authentication`, `LoginPage.jsx`, `Enable2fapage.jsx` |
| Dashboard | Đếm user/product/order; action center; doanh thu, trạng thái đơn; chọn khoảng ngày; xuất Excel/PDF | `Dashboard.cs`, `Stats.cs`, `DashboardPage.jsx`, `hooks/useDashboardData.js`, `components/dashboard` |
| Người dùng | Tìm/lọc/phân trang, xem chi tiết, duyệt/từ chối, khóa/mở khóa; lịch sử thao tác | `Users.cs`, `src/Application/Users`, `UsersPage.jsx`, `UserDetailPage.jsx` |
| Seller | Ví và hiệu suất; xếp hạng TopRated/AboveStandard/BelowStandard; cấu hình tiêu chí; đánh giá định kỳ | `src/Application/Sellers`, `SellersPage.jsx`, `SellerEvaluationBackgroundService.cs` |
| Sản phẩm | Danh sách, chi tiết, CRUD seller, báo cáo vi phạm; ẩn/cấm/khôi phục/từ chối report; cảnh báo seller | `Products.cs`, `src/Application/Products`, `ProductModerationPage.jsx`, `SellerProductManagementPage.jsx` |
| Review | Tạo, seller reply, report; hàng chờ kiểm duyệt; ẩn/giữ; giới hạn người vi phạm; gọi AI | `Reviews.cs`, `src/Application/Reviews`, `ReviewMonitoringPage.jsx` |
| Đơn hàng | Danh sách toàn hệ thống, tìm theo ID/người mua, lọc trạng thái, phân trang, xem chi tiết | `Orders.cs`, `src/Application/Orders`, `OrdersPage.jsx` |
| Dispute | Dashboard, docket, chi tiết/bằng chứng, nhận xử lý, phán quyết Buyer/Seller/Split; điều chỉnh ví và log | `Disputes.cs`, `src/Application/Admin/Disputes`, `DisputesPage.jsx`, `DisputeDetailPage.jsx` |
| Hoàn trả | Danh sách/chi tiết/tin nhắn, phán quyết, duyệt/từ chối/đóng băng, nhãn trả hàng; tự escalation sau 3 ngày | `AdminReturnRequests.cs`, `src/Application/ReturRequests`, `ReturnRequestDetailPage.jsx` |
| Tài chính | Số dư pending/available/locked/disputed, quyết toán, phí nền tảng, payout, lịch sử và giải phóng hold | `Financials.cs`, `Wallets.cs`, `src/Application/Financials`, `src/Application/Payouts`, `PayoutEnginePage.jsx` |
| Thông báo | Broadcast All/Buyer/Seller/Group, Email/InApp, lưu lịch gửi, trạng thái đã đọc, SignalR | `Broadcasts.cs`, `src/Application/Broadcasts`, `BroadcastPage.jsx`, `AdminNotifications.jsx` |
| Quản trị | Tạo role/admin, gán role, audit log có lọc, theo dõi IP hoạt động | `AdminRoles.cs`, `AuditLogs.cs`, `SystemAdmin.cs` và các page tương ứng |
| Demo | Seed sẵn dữ liệu; trang mock tạo user/order/dispute/return/defect, đẩy payout và tăng tốc settlement | `src/Infrastructure/Data/Seeders`, `Mocking.cs`, `MockPage.jsx` |

Các đường dẫn page ở bảng trên nằm trong `src/Web/ClientApp/src/pages`; các endpoint nằm trong `src/Web/Endpoints`. Folder `ReturRequests` là tên đang được dùng trong source.

## 3. Đối chiếu đủ 14 yêu cầu Admin

| # | Yêu cầu | Trạng thái | Đã có và phần còn thiếu |
|---|---|---|---|
| A1 | Dashboard: số user, sản phẩm, đơn hàng | Có triển khai | `GetDashboardMetricsQuery` truy vấn DB, `MetricTiles` hiển thị, `DashboardPage` nối API và chọn khoảng ngày. Các tổng có thể theo khoảng lọc, cần thống nhất nhãn với ý nghĩa tổng toàn hệ thống. |
| A2 | Danh sách user, duyệt mới, khóa/mở khóa | Có triển khai | Đủ endpoint, handler và UI. `BanUser` chặn khóa khi còn đơn chưa kết thúc. Có auto-review tài khoản trùng CCCD/IP/tọa độ. Cần kiểm tra quyền giữa Support và SuperAdmin cùng tính nhất quán nguồn tài khoản Identity/domain User. |
| A3 | Xóa/ẩn sản phẩm vi phạm | Có nghiệp vụ; một phần về bảo vệ API | `ResolveProductViolation` hỗ trợ DeleteAndWarn (soft delete bằng Banned), Hide, Reject, Restore; có UI và email/SignalR. Endpoint xử lý vi phạm chưa được ràng buộc policy admin. API DeleteProduct riêng là hard delete dành cho chính seller, không phải đường admin xóa vi phạm. |
| A4 | Quản lý đơn hàng toàn hệ thống | Một phần / cần chốt phạm vi | Có tìm/lọc/phân trang và xem chi tiết. `Orders.cs` chỉ map GET; chưa có thao tác admin cập nhật/hủy đơn. Nếu yêu cầu chỉ là giám sát thì phần đọc đã có; nếu bao gồm điều hành trạng thái thì còn thiếu. Policy ManageOrders khai báo cho Support nhưng endpoint chỉ nhận Administrator/SuperAdmin. |
| A5 | Thống kê ngày/tháng/quý: doanh thu, đơn, user mới | Một phần | UI có preset today/week/month/quarter/custom; API có doanh thu theo ngày, user mới theo ngày và tổng đơn theo trạng thái trong khoảng ngày. Chưa có tổng hợp chuỗi tháng/quý cho cả ba chỉ số; `stats.users` được tải nhưng DashboardPage chưa hiển thị biểu đồ/tile tăng trưởng user. Có lỗi biên ngày/múi giờ và khác biệt định nghĩa doanh thu, xem mục 5. |
| A6 | Giám sát Review + Feedback | Một phần | Review có luồng report/AI/ẩn/giữ và seller reply. `Feedback` mới là entity điểm tổng hợp seller, được đọc trong chi tiết dispute; chưa thấy màn/API giám sát Feedback riêng hoặc cơ chế cập nhật tổng hợp đầy đủ từ review. Kiểm tra đã mua hàng trước khi review đang bị comment. |
| A7 | Dispute: duyệt, điều phối, phân xử | Một phần | Có docket, dashboard, chi tiết, assign và resolve Buyer/Seller/Split; lưu audit, cập nhật order/wallet, gửi thông báo. Endpoint assign hiện tự nhận cho user hiện tại, không nhận AdminId từ UI để điều phối cho admin khác. Group chỉ RequireAuthorization, chưa áp dụng ManageDisputes. Refund thực tế chưa tích hợp payment gateway. |
| A8 | Xét duyệt hoàn trả | Một phần | Duyệt/từ chối/phán quyết/đóng băng/cấp nhãn có handler và UI. Nhánh RequireReturn đi từ WaitingForReturnLabel đến ReturnLabelProvided; chưa thấy thao tác nhận hàng trả về rồi hoàn tiền. API chưa có policy admin nhất quán; hạch toán hoàn tiền trong DB chưa tương đương hoàn tiền thật. |
| A9 | Email cảnh báo hoặc thông báo toàn bộ user | Một phần | Có SMTP, broadcast tức thời theo đối tượng, InApp/SignalR và màn lịch sử. Email cảnh báo sản phẩm/review đã có lời gọi gửi. Email hẹn giờ chỉ lưu Scheduled, chưa thấy worker gửi khi đến hạn; các email Pending sau duyệt/khóa user cũng chưa thấy consumer. InApp hẹn giờ được truy vấn khi đến hạn. |
| A10 | Admin chỉ từ IP nội bộ hoặc xác minh 2FA | Một phần | Có InternalIpMiddleware và QR/TOTP. Middleware chặn mọi request ngoài allowlist trước authentication, nên 2FA không cho phép IP ngoài truy cập. FE bắt bật 2FA nhưng BE chưa có policy chứng minh đã hoàn thành 2FA cho API admin. Xử lý forwarded headers và bật 2FA cần hoàn thiện. |
| A11 | Phân quyền Monitor/Support/... rõ ràng | Một phần | Có role/policy, UI route guard, quản lý role/admin. Nhiều API chưa áp policy tương ứng; có API chỉ cần login hoặc chưa yêu cầu login. Permissions JSON của role chưa được dùng để thực thi quyền động. Có lệch quyền FE/BE. |
| A12 | Dashboard mở rộng được nhiều loại thống kê | Có triển khai ở mức cấu trúc | Widget được tách trong `components/dashboard`, hook/service/query riêng; có thể thêm thống kê theo pattern hiện có. Không thấy cơ chế widget động do admin cấu hình, nhưng Requirement.txt không bắt buộc tính năng đó. |
| A13 | Log riêng dữ liệu nhạy cảm, mã hóa khi lưu | Chưa đáp ứng đầy đủ | Có bảng AdminActions và trang AuditLogs. `Details` lưu string/JSON rõ; chưa thấy cơ chế mã hóa payload, luồng log nhạy cảm riêng hoặc cấu hình TDE/Always Encrypted trong repo. Không thể coi `Encrypt=True` của connection string hay password BCrypt là đáp ứng mã hóa log khi lưu. |
| A14 | UI dễ dùng cho admin không kỹ thuật | Có UI; cần nghiệm thu | Có menu, tab, tìm kiếm, phân trang, badge, modal xác nhận, toast và loading. Chưa có kết quả usability/accessibility test; không kết luận “đạt” chỉ từ source hoặc việc build thành công. |

## 4. Đối chiếu 6 yêu cầu bổ sung (nhóm 25% điểm)

| # | Yêu cầu | Trạng thái | Bằng chứng và việc còn thiếu |
|---|---|---|---|
| B1 | Scale/load balance/Nginx | Có cấu hình; chưa kiểm chứng tải | `k8s/api-deployment.yaml`: 2 API replicas; Service và Nginx reverse proxy/WebSocket; Redis SignalR backplane. Nginx proxy đến Service, Service phân phối sang pod. Chưa có kết quả tải hay bằng chứng scale thực tế. Các background job chạy ở mỗi API replica cần chống xử lý trùng. |
| B2 | Rate limiting theo người/IP trong khoảng thời gian | Một phần, chưa đúng mục tiêu | Có policy strict 5/phút, standard 60/phút, authenticated 200/phút và response 429. `AddFixedWindowLimiter`/`AddSlidingWindowLimiter` đang tạo giới hạn dùng chung theo policy, không partition theo IP/user. `GetPartitionKey` có khai báo nhưng không được gọi; nhiều endpoint không gắn policy, không có global limiter. Middleware rate limiter còn nằm trước authentication. |
| B3 | CI/CD Jenkins hoặc GitHub Actions | Một phần | Có `ci.yml`, `cd.yml`: restore/build backend, gọi unit test, build/push image GHCR. CI chỉ build Web rồi test project khác bằng --no-build, không build/restore test project trước trên runner sạch. CD chạy độc lập khi push main, `needs: []` không đợi CI và chưa deploy K8s. Có `azure-dev.yml` gọi azd provision/deploy nhưng chưa xác minh đã được cấu hình/chạy. Không bắt buộc bổ sung Jenkins nếu GitHub Actions được chấp nhận. |
| B4 | Quản lý team bằng Jira | Chưa thấy minh chứng Jira | `docs/StoryPoint-TimeTracking.md` mô tả quy trình ClickUp, không phải Jira. Cần board/backlog/sprint/phân công/lịch sử thực tế, hoặc xác nhận giảng viên cho dùng ClickUp tương đương. Không suy ra team chưa quản lý công việc chỉ vì thiếu Jira trong source. |
| B5 | Kiểm thử tải và an ninh bằng JMeter | Chưa thấy minh chứng | Chưa tìm thấy test plan `.jmx`, script/kịch bản JMeter, báo cáo tải hay báo cáo kiểm thử an ninh. Unit test/health check và module Bicep loadtesting không thay thế kết quả kiểm thử này. |
| B6 | Zero downtime khi cập nhật bằng K8s | Một phần | Có RollingUpdate, maxUnavailable: 0, maxSurge: 1, readiness/liveness `/health`, 2 replicas; tài liệu `docs/RollingDeployment.md` hướng dẫn rollout/rollback thủ công. Chưa có số liệu request liên tục trong rollout; startup mỗi pod migrate+seed DB và chạy job nền; DB/Redis vẫn đơn replica. Cấu hình rolling update là nền tảng, chưa chứng minh toàn hệ thống không downtime. |

## 5. Những khoảng thiếu cần ưu tiên

### P1 — Quyền truy cập và danh tính người thao tác

1. **Khóa quyền tại backend cho thao tác admin.** `Products.cs`, `Reviews.cs`, `AdminReturnRequests.cs` không yêu cầu authorization chung; các handler kiểm duyệt tương ứng không có attribute/policy bù lại. `Disputes.cs`, Dashboard/Stats và một số Financials action chỉ yêu cầu login. `WebApplicationExtensions.cs` không tự thêm policy chung. IP allowlist không thay thế phân quyền người dùng.
2. **Đồng bộ quyền FE/BE.** Route Broadcast chỉ cho SuperAdmin nhưng API ManageBroadcasts cho Support; route Orders không giới hạn role nhưng API không cho Support/Monitor. Dashboard hiển thị widget gọi API cấu hình phí cho các role không có quyền gọi API đó. Policy khai báo chưa có nghĩa đã được áp dụng.
3. **Lấy AdminId từ phiên xác thực.** Nhiều endpoint nhận AdminId/CreatedBy/AssignedBy từ body. `ReviewMonitoringPage.jsx` gửi cứng adminId: 1; `adminSession.js` có fallback 1. Audit có thể ghi sai người thao tác dù tính năng đã có.
4. **Hoàn thiện 2FA.** `Enable2FACommandHandler` bật TwoFactorEnabled ngay lúc tạo QR; Verify2FASetup chỉ trả kết quả kiểm tra. Verify2FA login nhận UserId+Code mà chưa gắn với challenge đã xác minh mật khẩu. Cần đảm bảo xác thực hai yếu tố thực sự, trạng thái enrollment đúng và secret được bảo vệ.
5. **Xác định mô hình IP/2FA đúng yêu cầu.** Hiện mọi trang public/API cũng bị allowlist; IP ngoài bị chặn dù có OTP. Cấu hình forwarded headers xóa danh sách proxy tin cậy và bỏ giới hạn hop; cần ràng buộc proxy trước khi dựa vào IP để kiểm soát truy cập. So khớp wildcard hiện là prefix đơn giản.
6. **Log nhạy cảm:** xây dựng đường log riêng và mã hóa khi lưu theo A13; rà soát cả TwoFactorSecret, CCCD, thông tin ngân hàng và dữ liệu trước/sau trong audit. `LoggingBehaviour` đang log toàn bộ request và được đăng ký làm MediatR preprocessor; các record LoginCommand/CreateAdminUserCommand có Password, Verify2FACommand có Code. Đây là đường có nguy cơ đưa mật khẩu/OTP vào log thường, cần che hoặc loại bỏ trước khi ghi. Chưa xác minh cấu hình mã hóa ngoài repository.

### P2 — Hoàn tất nghiệp vụ đang dở

7. **Thống kê:** `useDashboardData.js` chuyển ngày local thành ISO UTC rồi cắt yyyy-MM-dd; có thể lệch ngày ở múi giờ Việt Nam. EndDate gửi một ngày và backend dùng `<= EndDate`, dễ bỏ dữ liệu phát sinh sau đầu ngày cuối. Cần thống nhất khoảng ngày và timezone rồi kiểm thử ngày/tháng/quý. API `GetAdminDashboardStats` còn tính NewUsersThisMonth bằng ApprovedAt dù User có CreatedAt.
8. **Định nghĩa doanh thu:** Stats lấy giao dịch FeeDeduction (doanh thu phí nền tảng), còn WeeklyRevenue trong GetDashboardMetrics cộng TotalPrice của đơn, không cùng chỉ số. Cần thống nhất hoặc đặt tên rõ. Đưa user growth lên UI nếu yêu cầu thống kê người dùng mới phải nhìn thấy trên dashboard.
9. **Review/Feedback:** hoàn thiện phần Feedback theo cách hiểu được chấp thuận; bật lại kiểm tra người đã mua hàng, ràng buộc ReviewerId/SellerId/ReporterId với danh tính đăng nhập. Hiện UI có kiểm tra nhưng request có thể gửi ID khác.
10. **Dispute:** bổ sung luồng giao case cho admin khác nếu cần “điều phối”; kiểm tra giới hạn refund so với giá trị được phép. Mã giao dịch refund hiện được mô phỏng bằng GUID và hạch toán ví, không có chuyển tiền về ngân hàng của buyer.
11. **Return:** hoàn tất RequireReturn → nhận hàng → refund/đóng yêu cầu; kiểm tra số tiền hoàn (hiện ưu tiên SellerEarnings), trường hợp không tìm thấy ví seller, số dư khóa và audit ở từng nhánh. Không nên xem trạng thái Refunded trong DB là bằng chứng buyer nhận tiền.
12. **Email:** có consumer/worker cho Scheduled và Pending nếu giữ tính năng đó; trạng thái gửi phải phản ánh kết quả SMTP. EmailService hiện catch lỗi hoặc thiếu cấu hình rồi return, nên caller có thể coi là gửi thành công.
13. **Orders:** chỉ bổ sung thao tác thay đổi đơn nếu phạm vi “quản lý” yêu cầu; không cần xây toàn bộ checkout để hoàn thành yêu cầu Admin hiện tại.

### P3 — CI, tải và nhiều replica

14. **CI chạy được trên runner sạch:** restore/build test project trước test --no-build; kiểm thử frontend cũng cần được nối vào pipeline nếu dùng để chặn lỗi giao diện. Domain test đang continue-on-error nên có thể thất bại mà CI vẫn xanh.
15. **CD:** đợi CI thành công trước publish/deploy; nếu cần tự động triển khai thì thêm bước rollout/verify với tag SHA. Workflow Azure hiện có cũng cần bằng chứng cấu hình và chạy thành công, không nên tính mặc định là CD đã vận hành.
16. **Nhiều pod:** settlement/payout/evaluation/auto-review/escalation đăng ký như hosted service trong mỗi API instance. Chưa thấy khóa phân tán hoặc job leader. Payout kiểm tra AnyAsync(Processing) rồi mới ghi không đủ bảo đảm chống trùng khi hai instance kiểm tra đồng thời. Cần test concurrency với DB thật.
17. **Lịch payout:** UI/API lưu Frequency/ScheduledHourUtc nhưng `PayoutEngineBackgroundService` đang cố định 02:00 UTC mỗi ngày; thay đổi cấu hình giờ/tần suất chưa điều khiển được lịch chạy.
18. **Bằng chứng yêu cầu bổ sung:** Jira hoặc chấp thuận ClickUp; JMeter plan và report; đo lỗi/latency trong rollout K8s và chứng minh request sang nhiều pod. Không chỉ chụp file cấu hình.

## 6. Kiểm chứng đã thực hiện

| Lệnh / kiểm tra | Kết quả |
|---|---|
| `dotnet build src/Web/Web.csproj --no-restore -c Release /p:SkipNSwag=True` | Thành công, 0 warning, 0 error. Đây là build backend; không phải kiểm tra startup/DB hay build React. |
| `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore -c Release --verbosity minimal` | 28 passed, 0 failed, 0 skipped. |
| `dotnet test tests/Domain.UnitTests/Domain.UnitTests.csproj --no-restore -c Release --verbosity minimal` | 5 passed, 0 failed, 0 skipped. Nhóm này chủ yếu là Colour của template, không chứng minh nghiệp vụ thương mại điện tử. |
| `npm.cmd run build` tại `src/Web/ClientApp` | Thành công (exit 0), compiled with warnings: unused variables, hook dependencies, accessibility, key trùng trong endpoints.js; bundle chính khoảng 648.23 kB gzip. Chạy `npm.cmd` vì PowerShell chặn npm.ps1 theo execution policy; không thay đổi policy máy. |

Các test Application hiện tập trung vào login/2FA, user, product, review, dashboard stats và settlement. `WithdrawalUnitTests.cs` đang comment. Chưa thấy test riêng bao phủ đầy đủ Dispute, Return, Broadcast, RBAC/IP, rate limiting và đồng thời nhiều instance. Functional/Integration project chủ yếu là hạ tầng test; Acceptance Login còn trỏ `/Identity/Account/Login`, không phải luồng React `/login` + OTP hiện tại. Frontend có smoke test render App, chưa phải test nghiệp vụ đầy đủ.

Không khởi động web hoặc thao tác DB trong đợt rà soát: `Program.cs` luôn migrate và seed khi startup, có thể làm thay đổi dữ liệu. Chưa xác minh SMTP/AI bên ngoài, browser end-to-end, Docker/K8s/Azure thật, GitHub Actions run, Jira hay hiệu năng/an ninh dưới tải. Chỉ thêm tài liệu đánh giá, không sửa code chức năng.

## 7. Kết luận để theo dõi tiến độ

Phần Admin đã có nền tảng và đa số màn nghiệp vụ; không phải project mới chỉ có skeleton. Tuy nhiên, không nên báo “đã hoàn thành toàn bộ Requirement.txt”: bảo vệ API và log mã hóa chưa đầy đủ, thống kê cần chỉnh nghĩa/biên ngày, một số luồng hoàn trả/email còn dở, và nhóm yêu cầu vận hành mới có cấu hình cùng tài liệu là chính.

Thứ tự hợp lý: **phân quyền + danh tính/2FA/IP + log nhạy cảm → hoàn tất các luồng nghiệp vụ đang dở → sửa pipeline → bổ sung test tải/an ninh và chứng minh rollout/scale**. Các phân hệ mở rộng như payout/AI hữu ích cho demo nhưng không thay thế các tiêu chí bắt buộc còn thiếu.
