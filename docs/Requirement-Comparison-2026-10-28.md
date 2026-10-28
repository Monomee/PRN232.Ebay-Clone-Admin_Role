# So sánh sau khi pull main — 28/10/2026

## Mốc so sánh

- Bản đã được rà soát trước: nhánh `hoangnv`, commit `423648b` (commit lưu tài liệu đánh giá).
- Bản hiện tại: nhánh `main`, commit `18bf623`, cùng mốc với `origin/main` ở thời điểm kiểm tra.
- Reflog ghi nhận chuyển sang main tại `728a507`, sau đó pull fast-forward đến `18bf623`.
- So sánh bằng `git diff 423648b 18bf623`: 85 file thay đổi nếu loại `tests/jmeter/results/**`; thư mục bị loại chứa nhiều tài nguyên HTML/JS của báo cáo JMeter tự sinh. Không hiểu số file báo cáo tự sinh là số tính năng mới.
- Báo cáo cũ `docs/Project-Requirement-Review.md` có trong commit `423648b` nhưng không có trong main hiện tại. Đã đọc bản lịch sử để đối chiếu, không khôi phục hay sửa code nghiệp vụ.

“Đã giải quyết” dưới đây giới hạn ở đầu việc cụ thể được nêu. Có thêm kiểm thử hoặc giao diện không đồng nghĩa toàn bộ yêu cầu nghiệp vụ/bảo mật đã đạt.

### Cách dùng tài liệu để theo dõi task

- **Mục 1 — Đã làm thêm:** ghi nhận phần code, cấu hình, test và tài liệu đã được bổ sung sau pull. Có thể đóng đầu việc nhỏ như “viết màn Feedback”, nhưng không tự động đóng đầu việc lớn “hoàn thiện Review + Feedback”.
- **Mục 2 — Đã làm nhưng chưa nên đóng:** xác định phần đã có, phần còn thiếu và điều kiện nghiệm thu của từng nhóm đang dở.
- **Mục 3 — Cần làm thêm:** backlog có mã `TODO-xx`, mức ưu tiên, phạm vi, điều kiện đóng và bằng chứng cần lưu. Đây là mã theo dõi trong tài liệu này, không phải mã Jira/ClickUp đã tồn tại.
- **Mục 4–7:** giữ số liệu kiểm thử, đối chiếu báo cáo cũ, rủi ro cụ thể và giới hạn kiểm chứng.
- **P0:** cần xử lý trước khi dùng dữ liệu thật/cho truy cập ngoài môi trường demo; **P1:** cần hoàn thiện để nghiệm thu chức năng; **P2:** minh chứng vận hành, kiểm thử và chất lượng giao diện. Mức ưu tiên là đề xuất từ lần rà soát, không phải thứ tự nhóm đã cam kết.

Điều kiện đóng task nên gồm **code đúng + kiểm tra hành vi + bằng chứng**, không chỉ commit có tên “fix”, giao diện xuất hiện hoặc build xanh. Những kết quả build/test ở mục 7 thuộc lượt kiểm tra bản `18bf623`; lượt cập nhật tài liệu này không chạy lại ứng dụng.

## 1. Những việc đã làm thêm sau khi pull

| Nhóm / commit | Trước | Hiện tại | Đánh giá task |
|---|---|---|---|
| CI — `7bad21a` | Build Web rồi chạy test project khác với --no-build | Bỏ --no-build ở Application/Domain test để dotnet test tự restore/build | **Đã sửa lỗi cấu hình chạy unit test trên runner sạch.** Chưa xác minh workflow GitHub thực tế; Domain test vẫn continue-on-error. |
| Functional test — `3ffed58`, `1d45610` | Chủ yếu có khung test | Test host môi trường Testing, SQL Server Testcontainers, smoke DB, 3 ca user moderation, chạy functional test trong CI | **Đã thêm hạ tầng và test tích hợp user moderation.** Local build thành công; chưa chạy vì terminal thiếu Docker. |
| Unit test W7-20 — `3c99986` | 28 Application tests; thiếu test các luồng rủi ro | Thêm 45 test Dispute, ReturnRequest, Payout/Withdrawal, Broadcast | **Đã hoàn tất phần bổ sung 45 unit test.** Chạy lại: 73/73 qua; không coi là bao phủ hết tiền tệ, bảo mật hoặc concurrency. |
| Load balance W7-09 — `e4bde9f` | Compose chỉ có một API; Nginx chủ yếu ở K8s | Cả Compose local/prod có api1, api2, Redis và Nginx reverse proxy; upstream round-robin và header X-Backend-Server | **Đã triển khai cấu hình Nginx + 2 API trong Compose.** Chưa có kết quả so sánh tải 1 vs 2 instance của bản này. |
| Rate limiting — `7538577` | Giới hạn tại ASP.NET dùng chung theo policy | Nginx có zone theo IP: API 25 req/s, login 12 req/phút, burst, JSON 429/Retry-After; app thêm header phân biệt nguồn 429; có script demo | **Đã bổ sung giới hạn theo IP ở Nginx Compose.** Lỗi partition ở ASP.NET còn nguyên; cấu hình Nginx trong k8s chưa có các zone mới này. |
| JMeter tải W7-18 — `e295dbe` | Không có plan/report trong nhánh đã rà soát | 00 smoke, 01 brute-force, 02 dashboard, 03 users/orders, 04 dispute; profile local/load-500, CSV, script chạy và kết quả baseline 100/200/500 users | **Đã có kịch bản và bằng chứng kiểm thử tải.** Đây là kết quả lịch sử tại commit 4983a90, không phải chạy lại bản 18bf623. |
| JMeter security W7-19 — `5ceba93` | Chưa có minh chứng kiểm thử IP/2FA/RBAC | Thêm plan 10/11/12, JTL, statistics.json, HTML report và danh sách SEC-01…SEC-07 | **Đã có kịch bản, kết quả và ghi nhận lỗi bảo mật.** Không phải task sửa xong các lỗi đó. |
| Rolling update — `8a16436`, `8fceae9` | Chung health probe; thiếu drain/shutdown và hướng dẫn migration cụ thể | preStop 15s, grace 45s, host shutdown 25s; tách /health/live và /health/ready; worker dừng khi cancellation; tài liệu expand/contract, script rollout v1→v2→undo | **Đã hoàn thiện nhiều thành phần phục vụ rollout.** Chưa chứng minh zero downtime bằng request liên tục trong lúc rollout. |
| Payout/worker — cùng nhóm rolling update | Có thể dừng sau khi khóa tiền trước khi hoàn tất | Ngừng nhận seller mới khi shutdown, lưu kết quả hoàn tất không bị cancellation; phiên sau tiếp tục Processing cũ >45s; lịch seller evaluation lưu cùng thay đổi seller | **Đã cải thiện khả năng dừng/khôi phục.** Chưa bảo đảm xử lý đúng một lần khi 2 instance cùng chạy. |
| SignalR — cùng nhóm rolling update | Có reconnect nhưng thiếu đọc lại dữ liệu khi nối lại | Dispute/notification/seller thêm callback onreconnected; caller tải lại danh sách/chi tiết/ví/thông báo | **Đã thêm tải lại dữ liệu sau reconnect.** Chưa chạy thử qua nhiều instance và rolling update. |
| Review/Feedback — `b2fb5dd`, `18bf623` | UI chủ yếu chỉ danh sách review bị gắn cờ | Hai tab Flagged Reviews và Seller Reputation; KPI, phân bố rating; lọc status/reason/rating/search; API statistics và seller-feedbacks, tìm seller/lọc tier/phân trang | **Đã thêm màn/API giám sát Feedback và mở rộng Review.** Chưa nối cập nhật bảng Feedback khi review thay đổi; quyền API và adminId cứng còn nguyên. |
| Sensitive logs — `7b5ad77`, `18bf623` | Audit string/JSON thường; chưa có màn log mã hóa riêng | Entity/DbSet SensitiveDataLog; AES-256-CBC + HMAC; API list/summary/decrypt chỉ SuperAdmin; route/menu/page lọc, xem ciphertext, giải mã và ghi audit giải mã | **Đã có màn/API và nền tảng log mã hóa.** Mới có seeder ghi log mẫu; chưa nối vào sự kiện nghiệp vụ thật, nên chưa đóng yêu cầu mã hóa log nhạy cảm toàn hệ thống. |

### Những đầu việc nhỏ có thể ghi nhận đã hoàn thành

| Đầu việc nhỏ | Bằng chứng hiện có | Giới hạn khi đánh dấu hoàn thành |
|---|---|---|
| Sửa lệnh CI chạy unit test | `.github/workflows/ci.yml` bỏ --no-build ở hai bước unit test | Đóng phần sửa cấu hình; vẫn phải xác minh run GitHub và phần CD riêng. |
| Bổ sung 45 Application unit test | Các folder `tests/Application.UnitTests/Disputes`, `ReturnRequests`, `Payouts`, `Broadcasts`; kết quả 73/73 qua | Đóng phần viết các test đã có; không đóng các nhánh chưa được test hoặc lỗi nghiệp vụ chưa sửa. |
| Viết bộ functional test user moderation | Testcontainers, test host, smoke DB và `UserModerationTests.cs`; build test project thành công | Đóng phần bổ sung code test; điều kiện “chạy thành công trên SQL Server” còn ở TODO-14. |
| Tạo màn/API Feedback | Hai tab trong `ReviewMonitoringPage.jsx`, query Feedback và statistics, endpoint/service tương ứng | Đóng phần màn hình và truy vấn; tính đúng dữ liệu động/quyền truy cập còn ở TODO-01/09. |
| Tạo màn/API Sensitive Logs | Route/menu/page, API list/summary/decrypt, AES/HMAC, audit giải mã | Chỉ đóng phần khung chức năng; chưa đóng log dữ liệu thật/mã hóa vận hành. |
| Thêm Nginx + hai API vào Compose | `docker-compose.yml`, `docker-compose.prod.yml`, `nginx/nginx.conf` | Đóng phần cấu hình; chưa đóng kiểm chứng cân bằng tải, SignalR và job nhiều instance. |
| Viết và lưu kết quả JMeter | Plan/CSV/profile/script/JTL/report đã có | Đóng phần tạo/chạy bộ đo lịch sử; không coi là kết quả của bản mới hoặc lỗ hổng đã được sửa. |
| Bổ sung thành phần rolling update | Probe, drain/shutdown, script demo, tài liệu expand/contract, callback SignalR reconnect | Đóng từng phần triển khai; zero downtime đầu cuối còn cần nghiệm thu. |

## 2. Những việc đã làm nhưng chưa nên đóng

### OPEN-01 — Log nhạy cảm riêng và mã hóa khi lưu (A13)

- **Đã làm:** SensitiveDataLog/DbSet, service AES-256-CBC/HMAC, dữ liệu mẫu mã hóa; list/filter/summary/decrypt chỉ SuperAdmin; ghi audit khi giải mã.
- **Chưa đủ:** thao tác nghiệp vụ thật chưa ghi vào bảng mới; LoggingBehaviour vẫn có thể ghi Password/Code trong request. Seeder tự dùng khóa mặc định thay vì service đã cấu hình, tạo bảng bằng SQL và có nhánh xóa bảng cũ. Chưa có migration tương ứng.
- **Chưa nên đóng vì:** chỉ chứng minh xem/giải mã dữ liệu demo, chưa chứng minh dữ liệu nhạy cảm phát sinh thật được log riêng và bảo vệ đầy đủ.
- **Điều kiện đóng:** hoàn tất TODO-05 và TODO-06; thao tác thật sinh log mã hóa; log thường không chứa secret; đổi cấu hình khóa không làm lệch khóa seeder/API; nâng cấp schema giữ nguyên dữ liệu cũ; người không có quyền bị từ chối.

### OPEN-02 — Review + Feedback monitoring (A6)

- **Đã làm:** màn hai tab, KPI/phân bố sao, lọc review và seller reputation; API/query mới, phân trang và tìm kiếm.
- **Chưa đủ:** bảng Feedback chưa thấy cơ chế cập nhật sau tạo/ẩn/khôi phục review; kiểm tra buyer đã mua hàng đang bị comment; API chưa bảo vệ đủ; UI gửi adminId: 1. Tier hiển thị từ điểm Feedback cần được phân biệt với SellerLevel tính theo hiệu suất bán hàng.
- **Chưa nên đóng vì:** số liệu seed hiển thị đúng chưa chứng minh dữ liệu sau thao tác thật tiếp tục đúng hoặc đúng người được thao tác.
- **Điều kiện đóng:** TODO-01/03/09; thêm hoặc đổi trạng thái review làm số liệu theo quy tắc đã thống nhất thay đổi đúng, reload vẫn đúng, không nhận đánh giá hay moderation mạo danh.

### OPEN-03 — Rate limiting theo người/IP (B2)

- **Đã làm:** Nginx Compose có zone theo IP, JSON 429, Retry-After và header nguồn; ASP.NET có các policy và script demo hai lớp.
- **Chưa đủ:** ASP.NET vẫn dùng limiter chung theo policy, GetPartitionKey chưa dùng, nhiều endpoint chưa gắn giới hạn; middleware nằm trước authentication. Hai API có bộ đếm riêng. Nginx K8s chưa có cấu hình giới hạn mới. Script dùng userName thay vì email và không assert kết quả.
- **Chưa nên đóng vì:** một người có thể làm người khác bị giới hạn ở lớp app, còn ngưỡng qua nhiều instance không giống ngưỡng trên một process.
- **Điều kiện đóng:** TODO-07; chứng minh hai IP/user không dùng nhầm quota, lớp cần bảo vệ trả 429/Retry-After đúng, script thất bại khi server không chặn đúng; ghi rõ ngưỡng nào áp ở gateway, ngưỡng nào ở mỗi instance.

### OPEN-04 — Load balance và vận hành hai API (B1)

- **Đã làm:** api1/api2 + Nginx + Redis trong Compose local/prod, upstream và header chỉ backend.
- **Chưa đủ:** chưa đo lại tải 1 vs 2 instance; nhiều hosted service vẫn chạy ở cả hai process; chưa kiểm chứng SignalR negotiate/reconnect, failover và hành vi ứng dụng khi một instance dừng.
- **Chưa nên đóng vì:** có hai container không tự chứng minh cân bằng tải hiệu quả hay dữ liệu không bị xử lý hai lần.
- **Điều kiện đóng:** TODO-08/16/17; có request sang cả hai API, dừng một API vẫn truy cập được, dữ liệu tiền/log/trạng thái nhất quán; báo cáo tải ghi môi trường và commit cụ thể.

### OPEN-05 — Zero downtime, shutdown và khôi phục payout (B6)

- **Đã làm:** live/ready probe riêng, preStop/grace/shutdown timeout, worker xử lý cancellation, payout tiếp tục dòng Processing cũ, reload dữ liệu sau reconnect, hướng dẫn migration và rollback.
- **Chưa đủ:** chưa chạy tải liên tục xuyên rollout; chưa kiểm tra tương thích schema trên DB có dữ liệu; dòng payout cũ >45s chưa được nhận xử lý độc quyền; chưa test shutdown ở các mốc trước/sau gateway.
- **Chưa nên đóng vì:** script hiện probe sau khi rollout kết thúc, không đo toàn bộ khoảng thay phiên bản. Một dòng Processing cũ không có nghĩa owner cũ đã ngừng chạy.
- **Điều kiện đóng:** TODO-08/17; bằng chứng request/đăng nhập/SignalR trong rollout và rollback, không mất/trùng hạch toán, không kẹt khóa tiền, image cũ chạy được với schema sau nâng cấp.

### OPEN-06 — CI/CD và functional test (B3)

- **Đã làm:** sửa --no-build, thêm functional test SQL Server Testcontainers và bước CI chạy chúng; bổ sung unit test.
- **Chưa đủ:** chưa xác minh CI run ở GitHub; Domain test vẫn continue-on-error; chưa có frontend check trong CI; CD không đợi CI và chưa tự deploy/verify K8s. Functional test local mới build, chưa chạy Docker.
- **Chưa nên đóng vì:** build/test project tại máy hiện tại không chứng minh pipeline trên runner sạch hoặc luồng phát hành an toàn hoạt động.
- **Điều kiện đóng:** TODO-14/15; run CI xanh có số test thực chạy, test bắt buộc thất bại thì chặn pipeline, CD chỉ chạy khi CI qua, rollout/verify có kết quả hoặc phạm vi CD thủ công được chấp thuận rõ.

### OPEN-07 — Kiểm thử tải/an ninh và sửa lỗi tìm được (B5)

- **Đã làm:** JMeter plan và kết quả lịch sử; có các ca IP/OTP/RBAC và danh sách SEC-01…SEC-07.
- **Chưa đủ:** report đang thuộc commit 4983a90, không phải bản 18bf623; IP còn 1 assertion lỗi, RBAC còn 3; một số rủi ro như verify OTP thiếu challenge mới được đọc từ source, chưa có test đầy đủ.
- **Chưa nên đóng vì:** có thể đóng task “chạy kiểm thử và ghi nhận lỗi”, nhưng chưa thể đóng task “hệ thống đạt yêu cầu an ninh/hiệu năng”.
- **Điều kiện đóng phần nghiệm thu:** sửa lỗi liên quan, chạy lại trên bản phát hành theo TODO-16; lưu JTL/report, commit/config/môi trường, phân biệt lỗi assertion với HTTP status kỳ vọng và không bỏ assertion để làm báo cáo xanh.

### OPEN-08 — Các luồng nghiệp vụ đã có test nhưng còn dở

- **Đã làm:** bổ sung test assign/resolve dispute, approve/reject return, broadcast và payout/withdrawal.
- **Chưa đủ:** điều phối case cho admin khác, giới hạn refund, nhánh nhận hàng trả rồi hoàn tiền, gửi email Scheduled/Pending và lịch payout theo cấu hình chưa được bổ sung tương ứng.
- **Chưa nên đóng vì:** test hiện tại xác nhận hành vi đang có; không tự tạo các bước nghiệp vụ còn thiếu.
- **Điều kiện đóng:** thực hiện TODO-10/11/12/13 theo phạm vi yêu cầu đã thống nhất và kiểm tra luồng đầy đủ từ UI đến DB/thông báo.

## 3. Những việc cần làm thêm — backlog và điều kiện đóng task

Các checkbox dưới đây đang để trống vì chưa có bằng chứng đầu việc hoàn tất tại mốc rà soát. Một task có thể liên quan nhiều yêu cầu; không cộng số lượng task này thành phần trăm hoàn thành project.

### 3.1. P0 — Bảo vệ dữ liệu, quyền truy cập và xử lý tiền

- [ ] **TODO-01 — Áp quyền cho từng API quản trị.**
  - **Phạm vi:** `src/Web/Endpoints/Products.cs`, `Reviews.cs`, `AdminReturnRequests.cs`, `Disputes.cs`, `Financials.cs`, Dashboard/Stats và các handler liên quan.
  - **Cần làm:** phân biệt route public/seller/buyer với route moderation/admin; áp policy phù hợp cho đọc dữ liệu quản trị và thao tác ghi. Không khóa toàn bộ API public chỉ vì cùng group với API admin. Các action settlement/payout phải có quyền tài chính tương ứng.
  - **Đóng khi:** anonymous nhận 401 ở API cần login; user đã login sai role nhận 403; đúng role gọi thành công. Request bị cấm không thay đổi dữ liệu. Chạy lại các ca RBAC đỏ và bổ sung ca Products/Reviews/Return.
  - **Bằng chứng:** ma trận endpoint × role, test HTTP qua middleware thật, report security sau sửa. Handler test đơn lẻ không chứng minh route đã được bảo vệ.

- [ ] **TODO-02 — Thống nhất quyền frontend/backend.**
  - **Phạm vi:** `AppRoutes.js`, `NavMenu.js`, các widget dashboard, `Policies.cs` và cấu hình policy/backend endpoint.
  - **Cần làm:** thống nhất quyền SuperAdmin/Support/Monitor cho Broadcast, Orders, Dashboard, Financials và SensitiveLogs; menu/nút/widget chỉ hiện hoặc cho thao tác theo quyền đã thống nhất. Làm rõ Permissions JSON đang là metadata hay quyền động; không để UI tạo permission nhưng backend bỏ qua mà vẫn mô tả là đã thực thi.
  - **Đóng khi:** mỗi role dùng được các chức năng được cấp, không thấy nút quản trị gây lỗi 403 vô cớ; gõ URL hoặc gọi API trực tiếp vẫn bị backend chặn đúng.
  - **Bằng chứng:** bảng quyền được nhóm chấp thuận và kết quả thử từng role. Thực hiện cùng/sau TODO-01.

- [ ] **TODO-03 — Lấy danh tính người thao tác từ phiên xác thực.**
  - **Phạm vi:** endpoint/command có AdminId, CreatedBy, AssignedBy; `ReviewMonitoringPage.jsx`, `services/adminSession.js`, AuditLog; nguồn tài khoản domain User/ASP.NET Identity.
  - **Cần làm:** backend tự lấy ID từ claims/CurrentUser; bỏ ID admin cố định và không tin ID trong body. Kiểm tra các lời gọi IdentityService đang nhận ID từ bảng domain User để tránh sai nhận diện SuperAdmin.
  - **Đóng khi:** login bằng hai admin khác nhau tạo audit đúng hai người; sửa AdminId/CreatedBy trong request không giả mạo được người thao tác; thiếu/malformed claim không âm thầm ghi về admin 1.
  - **Bằng chứng:** test chống mạo danh và dữ liệu audit trước/sau; không cần ghi lại mật khẩu hay token trong minh chứng.

- [ ] **TODO-04 — Hoàn thiện IP allowlist và 2FA.**
  - **Phạm vi:** `InternalIpMiddleware.cs`, cấu hình ForwardedHeaders trong `Web/DependencyInjection.cs`, `src/Application/Authentication`, Auth endpoint và ProtectedRoute.
  - **Cần làm:** chỉ tin proxy được xác định; thay prefix wildcard sai để 10.* không khớp 100.x; thống nhất chính sách “IP nội bộ hoặc 2FA” và phạm vi chỉ dành cho Admin. Tạo challenge tạm sau bước mật khẩu, bắt verify OTP dùng đúng challenge, hết hạn và không tái sử dụng; chỉ bật TwoFactorEnabled sau xác minh setup thành công. Backend phải thực thi điều kiện xác thực, không chỉ dựa redirect của React.
  - **Đóng khi:** giả X-Forwarded-For từ nguồn không tin cậy không vượt được whitelist; IP 100.x bị từ chối khi chỉ cho 10.*; có OTP nhưng chưa qua mật khẩu không login được; setup chưa xác minh chưa bật 2FA; đúng luồng và đúng chính sách IP/2FA vẫn login được.
  - **Bằng chứng:** test IP/OTP/challenge/enrollment và các case boundary; cập nhật, chạy lại JMeter security tương ứng.

- [ ] **TODO-05 — Ghi log nhạy cảm từ nghiệp vụ thật và loại secret khỏi log thường.**
  - **Phạm vi:** `LoggingBehaviour.cs`, các handler nghiệp vụ nhạy cảm, `SensitiveDataLog`, endpoint/query giải mã và AuditLog.
  - **Cần làm:** thống nhất danh sách sự kiện phải log, ví dụ thay đổi quyền và thao tác tài chính; ghi actor/target/thời điểm cùng payload mã hóa, hạn chế dữ liệu cần lưu. Che/bỏ Password, OTP, token và secret khỏi logging request/exception. Không hiểu yêu cầu “log nhạy cảm” là phải lưu mật khẩu/OTP rõ rồi cho admin xem lại.
  - **Đóng khi:** thao tác thật tạo log riêng truy vết được; DB không chứa payload nhạy cảm dạng rõ trong log đó; log console/file thông thường không lộ secret; chỉ SuperAdmin được giải mã và mọi lần giải mã có audit. Kiểm tra ciphertext/hmac sai không được trình bày như dữ liệu hợp lệ.
  - **Bằng chứng:** test sự kiện thật tạo log và test không rò secret; dữ liệu minh chứng dùng tài khoản giả. Phụ thuộc TODO-03/06.

- [ ] **TODO-06 — Sửa khóa mã hóa và nâng cấp schema log an toàn.**
  - **Phạm vi:** `AesEncryptionService.cs`, `SensitiveDataLogsSeeder.cs`, Infrastructure DI, EF Migrations, cấu hình môi trường.
  - **Cần làm:** seeder và API dùng cùng service/cấu hình khóa; đưa khóa vận hành ra ngoài source, không mặc nhiên dùng fallback cứng. Tạo migration cho bảng mới; bỏ nhánh DROP TABLE trong seed; xác định cách đọc dữ liệu đã mã hóa bằng khóa cũ nếu đã có dữ liệu cần giữ. HMAC lỗi hoặc giải mã lỗi phải có kết quả rõ và an toàn.
  - **Đóng khi:** DB mới tạo được schema qua migration; DB có dữ liệu cũ nâng cấp không mất log; khóa cấu hình thật mã hóa/giải mã được ở cả hai instance; khóa sai hoặc dữ liệu bị sửa bị phát hiện. Không đổi khóa đang dùng rồi vô tình làm mất khả năng đọc dữ liệu cũ.
  - **Bằng chứng:** test round-trip với khóa tùy chỉnh, wrong-key/tamper, migration trên bản sao DB có dữ liệu; không chỉ EnsureCreated. Cần làm trước khi hoàn tất TODO-05.

- [ ] **TODO-07 — Sửa rate limiting và script kiểm tra hai lớp.**
  - **Phạm vi:** `RateLimitingExtensions.cs`, thứ tự middleware trong Program, endpoint policy, `nginx/nginx.conf`, `k8s/nginx-config.yaml`, `scripts/test-rate-limit.ps1`.
  - **Cần làm:** áp partition theo IP/user cho lớp app; authentication phải có sẵn trước khi cần UserId. Xác định phạm vi áp dụng, quota theo mỗi instance hay toàn hệ thống; sử dụng gateway hiện có cho ngưỡng chung khi phù hợp. Đồng bộ cấu hình deploy cần hỗ trợ. Script gửi đúng email, có assert và exit code, không giả định request thứ 6 qua 2 API luôn chạm cùng một bộ đếm.
  - **Đóng khi:** hai người không dùng nhầm quota; đúng ngưỡng bị 429/Retry-After; không bỏ sót route cần bảo vệ; script trả lỗi nếu kỳ vọng không đạt; ghi rõ lớp nào giới hạn và cách đo trong cấu hình 1/2 instance.
  - **Bằng chứng:** kết quả theo hai IP/user và qua Nginx, kèm cấu hình/ngưỡng. Health probe không bị chặn ngoài ý muốn. Kết hợp TODO-04 để IP nguồn có thể tin cậy.

- [ ] **TODO-08 — Chống xử lý trùng khi chạy nhiều API và phục hồi payout.**
  - **Phạm vi:** RunPayoutEngine/ResumeStaleProcessingAsync, settlement, seller evaluation, auto-review/escalation và các hosted service; wallet/transaction trong SQL Server.
  - **Cần làm:** có cơ chế nhận việc nguyên tử/khóa phù hợp ở nơi cần chạy một lần; không dùng AnyAsync rồi ghi làm bảo đảm độc quyền. Không nhận lại payout chỉ dựa tuổi >45s khi owner có thể còn sống. Xác định kết quả giao dịch trước khi thử lại; hiện gateway là mock, chưa được coi an toàn cho chuyển tiền thật.
  - **Đóng khi:** hai process cùng chạy trên một seller/order/payout không gọi/hạch toán trùng; restart trước/sau khóa tiền và trước/sau gateway không mất tiền, không kẹt trạng thái; lỗi không bị nuốt rồi báo FundsCleared khi chưa thực sự chuyển số dư. Kiểm tra đồng thời với DB thật.
  - **Bằng chứng:** test nhiều context/process, số lần gọi gateway giả lập, giao dịch và số dư trước/sau, kết quả restart. Là điều kiện quan trọng của OPEN-04/05.

### 3.2. P1 — Hoàn tất nghiệp vụ và số liệu

- [ ] **TODO-09 — Giữ Review/Feedback đúng sau thao tác thật.**
  - **Phạm vi:** CreateReview/ReplyToReview/ReportReview/UpdateReviewStatus, GetSellerFeedbacks/GetReviewStatistics, ReviewMonitoringPage, Feedback entity.
  - **Cần làm:** thống nhất review nào tham gia điểm tổng hợp, cách tính AverageRating/PositiveRate/TotalReviews, cập nhật hoặc truy vấn lại đúng sau tạo/ẩn/khôi phục. Khôi phục kiểm tra người đã mua hàng theo quy tắc nghiệp vụ; lấy ReviewerId/SellerId/ReporterId từ danh tính khi áp dụng. Làm rõ tier reputation và seller performance level để không dùng nhầm.
  - **Đóng khi:** dữ liệu không phụ thuộc seed; tạo/ẩn/giữ review cập nhật điểm đúng và không cộng/trừ lặp khi thao tác lại; người không mua hoặc seller không sở hữu không thực hiện được thao tác trái phép; filter/search/page vẫn đúng sau cập nhật.
  - **Bằng chứng:** test dữ liệu tối thiểu tính được kết quả bằng tay, case không có review/giá trị rỗng, kiểm tra UI và API. Phụ thuộc TODO-01/03.

- [ ] **TODO-10 — Hoàn thiện điều phối và phán quyết Dispute.**
  - **Phạm vi:** Disputes endpoint/service/page, AssignDisputeCommand, ResolveDisputeCommand, wallet/order/transaction liên quan.
  - **Cần làm:** nếu “điều phối” bao gồm phân cho admin khác thì có chọn người đủ quyền và gửi AdminId được kiểm tra; hạn chế chuyển trạng thái case đã đóng; ràng buộc số tiền hoàn theo giá trị được phép. Ghi đúng người phân/xử và cập nhật order/wallet/thông báo nhất quán.
  - **Đóng khi:** đúng role nhận/chuyển case được, sai role bị chặn; Buyer/Seller/Split cho kết quả tiền đúng; refund vượt giới hạn bị từ chối; gửi resolve lại hoặc hai admin resolve đồng thời không hoàn/hạch toán hai lần.
  - **Bằng chứng:** test chuyển case, refund biên và đồng thời; kiểm tra UI → API → DB. Refund ngân hàng thật chỉ bổ sung nếu phạm vi bài yêu cầu; hiện phải ghi rõ mô phỏng, không coi GUID là chứng từ chuyển tiền.

- [ ] **TODO-11 — Hoàn tất nhánh trả hàng bắt buộc rồi hoàn tiền.**
  - **Phạm vi:** folder `src/Application/ReturRequests`, AdminReturnRequests endpoint, ReturnRequestDetailPage và ví/giao dịch.
  - **Cần làm:** nối RequireReturn → cấp nhãn → nhận hàng → hoàn tiền/đóng yêu cầu; xác định ai xác nhận nhận hàng và bằng chứng. Thống nhất hoàn theo tiền buyer đã trả hay SellerEarnings, phí ai chịu, nguồn eBay fund; xử lý thiếu ví/thiếu số dư rõ ràng, không báo thành công khi chưa hạch toán được.
  - **Đóng khi:** đi hết nhánh RequireReturn qua UI mà không sửa DB tay; không hoàn trước điều kiện nhận hàng; không hoàn lặp; reject/freeze có trạng thái/audit/thông báo hợp lệ; các nhánh hoàn không cần trả hàng vẫn hoạt động.
  - **Bằng chứng:** test toàn chuỗi và case không đủ tiền/không có ví, số dư/giao dịch trước sau, audit đúng admin. Phụ thuộc TODO-01/03/08 cho quyền và tính nhất quán.

- [ ] **TODO-12 — Gửi email Pending/Scheduled và phản ánh đúng kết quả.**
  - **Phạm vi:** SendBroadcast, Notification, các lệnh approve/ban user, EmailService và worker gửi.
  - **Cần làm:** consumer/worker lấy email đến hạn hoặc pending; xử lý riêng InApp đã có truy vấn đến hạn. Cập nhật trạng thái theo kết quả SMTP, không báo gửi thành công khi thiếu cấu hình hoặc catch lỗi. Xác định retry và tránh hai instance cùng gửi một thông báo; không mất tác vụ nền khi process restart.
  - **Đóng khi:** email gửi ngay, hẹn giờ và email sau duyệt/khóa user đi đúng đối tượng, đúng hạn; SMTP lỗi có trạng thái thất bại/retry truy vết được; restart hoặc hai worker không gửi trùng ngoài chính sách đã định.
  - **Bằng chứng:** test bằng SMTP giả lập/capture dùng dữ liệu demo; log kết quả và trạng thái DB, không cần gửi thử tới người dùng thật.

- [ ] **TODO-13 — Sửa thống kê và lịch payout theo cấu hình.**
  - **Phạm vi:** Stats/Dashboard queries, `useDashboardData.js`, RevenueChart/MetricTiles, PayoutConfig/UpdatePayoutConfig và PayoutEngineBackgroundService.
  - **Cần làm phần thống kê:** thống nhất timezone, khoảng ngày bao trọn ngày cuối; thống nhất doanh thu phí nền tảng và tổng giá trị đơn, đặt nhãn đúng; user mới theo CreatedAt thay vì ApprovedAt khi yêu cầu là đăng ký mới; hiển thị user growth đã tải. Thống nhất yêu cầu ngày/tháng/quý là lọc khoảng thời gian hay chuỗi tổng hợp để triển khai vừa đủ.
  - **Cần làm phần payout:** đọc ScheduledHourUtc/Frequency đã lưu thay vì cố định 02:00 UTC; validate cấu hình và xác định khi nào thay đổi có hiệu lực.
  - **Đóng khi:** dữ liệu ở đầu/cuối ngày, tháng, quý và timezone Việt Nam được tính đúng; số trên UI/export khớp DB theo định nghĩa; đổi giờ/tần suất payout thay đổi lịch thực tế; IsEnabled được tôn trọng theo phạm vi đã định.
  - **Bằng chứng:** bộ dữ liệu biên thời gian tính được bằng tay, test lịch không phải đợi giờ thật, đối chiếu UI/Excel. Có thể tách hai subtask thống kê và lịch payout khi phân công.

### 3.3. P1/P2 — Pipeline và bằng chứng nghiệm thu

- [ ] **TODO-14 — Chạy được bộ test mới trên runner sạch.**
  - **Ưu tiên:** P1 cho kiểm thử bắt buộc.
  - **Phạm vi:** ci.yml, Application.FunctionalTests, Application/Domain unit tests, ClientApp scripts.
  - **Cần làm:** chạy thực functional test bằng Docker/Testcontainers; lưu số ca chạy và xác minh test dùng DB riêng; quyết định Domain test nào bắt buộc thay vì continue-on-error vô điều kiện; nối frontend build/check cần thiết vào CI. Thêm test trực tiếp cho AES/seeder, Feedback, RBAC và payout stale/concurrency sau khi sửa.
  - **Đóng khi:** runner mới restore/build/test đầy đủ; không có bước xanh do không chạy test; lỗi ở test bắt buộc làm pipeline thất bại; không migrate/seed DB người dùng trong test.
  - **Bằng chứng:** URL/artifact CI run, số test và kết quả; Docker local hoặc runner có sẵn là môi trường kiểm thử, không phải DB production.

- [ ] **TODO-15 — Nối CD với kết quả CI và xác minh triển khai.**
  - **Ưu tiên:** P1 nếu yêu cầu CD tự động.
  - **Phạm vi:** cd.yml, azure-dev.yml nếu chọn Azure, Dockerfile và script triển khai K8s.
  - **Cần làm:** chọn đường phát hành thực dùng, CD chỉ tiếp tục khi CI thành công; dùng image tag xác định theo commit; deploy rồi kiểm tra rollout/readiness và smoke test. Nếu chỉ delivery image, mô tả đúng phạm vi và có quy trình deploy/rollback được chấp thuận, không gọi đó là tự deploy đã hoàn tất.
  - **Đóng khi:** commit lỗi không được phát hành qua luồng đó; phiên bản đúng được deploy; lỗi readiness/smoke được phát hiện; rollback dùng đúng phiên bản. Việc thực thi deploy cần theo quyền và môi trường nhóm cho phép.
  - **Bằng chứng:** CI/CD run, tag/image digest, rollout/smoke/rollback log; không chỉ comment “chờ CI” trong YAML.

- [ ] **TODO-16 — Chạy lại JMeter trên bản sửa và so sánh scale.**
  - **Ưu tiên:** P2, sau sửa P0.
  - **Phạm vi:** tests/jmeter/plans, config/data/scripts/results, README; Compose một/hai API.
  - **Cần làm:** chạy lại security giữ assertion đúng, theo dõi SEC-01…SEC-07; bổ sung case các lỗi đã sửa nhưng plan chưa bao phủ. So sánh tải 1 và 2 API trên cùng dữ liệu/cấu hình/tài nguyên, đặc biệt Dispute detail từng có p95 ~2,65s tại 500 users; ghi CPU/RAM API và SQL để xác định nút thắt.
  - **Đóng khi:** có report gắn commit mới; không còn ca security đỏ thuộc phạm vi bắt buộc; số liệu lỗi/p95/throughput được so với tiêu chí nhóm chấp thuận. Nếu scale không cải thiện, báo đúng nguyên nhân thay vì chỉ báo đã có hai container.
  - **Bằng chứng:** JTL/HTML/statistics, lệnh và config chạy, phiên bản công cụ, dữ liệu seed, tài nguyên, số instance, commit và bảng so sánh; không dùng report 4983a90 làm kết quả của 18bf623.

- [ ] **TODO-17 — Chứng minh zero downtime và SignalR khi rollout/rollback.**
  - **Ưu tiên:** P2, sau TODO-06/08 và chuẩn bị môi trường.
  - **Phạm vi:** k8s deployment/probe, scripts/demo-rolling-update.sh, RollingUpdate/ExpandContractMigrations docs và các client SignalR.
  - **Cần làm:** phát request liên tục trước/trong/sau rollout v1→v2 và rollback, không chỉ probe khi rollout đã xong. Theo dõi login/session, API quan trọng, SignalR disconnect/reconnect và tải lại dữ liệu; kiểm tra worker đang xử lý, schema tương thích và dữ liệu sau rollback.
  - **Đóng khi:** trong phạm vi môi trường được nghiệm thu không có khoảng mất phục vụ; lỗi/timeout được đo minh bạch; request đang chạy được xử lý theo chính sách shutdown; ví/giao dịch không mất/trùng, không mất trạng thái UI sau reconnect. Sự kiện reconnect tạm thời phải được mô tả đúng, không gọi mọi kết nối WebSocket là không bao giờ ngắt.
  - **Bằng chứng:** chuỗi thời gian request và status/latency, pod/image/rollout log, kiểm tra dữ liệu trước/sau, trường hợp rollout lỗi và rollback. DB/Redis đơn replica vẫn là giới hạn của toàn hệ thống, không được che bằng kết quả API rolling update.

- [ ] **TODO-18 — Bổ sung minh chứng quản lý nhóm và nghiệm thu UI.**
  - **Ưu tiên:** P2; phần công cụ quản lý cần xác nhận với tiêu chí môn học.
  - **Phạm vi:** board Jira/ClickUp thực tế, docs/StoryPoint-TimeTracking.md, các màn Admin.
  - **Cần làm:** có backlog/sprint/phân công/trạng thái/lịch sử; Requirement.txt ghi Jira trong khi tài liệu hiện mô tả ClickUp, nên cần minh chứng Jira hoặc chấp thuận dùng công cụ tương đương. Kiểm tra các thao tác thường dùng với admin không chuyên kỹ thuật, lỗi/toast/loading, nhãn số liệu, bàn phím/label cơ bản và màn hình nhỏ; xử lý cảnh báo liên quan hành vi/a11y theo mức ảnh hưởng.
  - **Đóng khi:** người nghiệm thu truy vết được task–người làm–commit–kết quả; các luồng Admin được thực hiện rõ ràng, không phải hiểu code mới dùng được; có ghi nhận vấn đề và kết quả kiểm lại.
  - **Bằng chứng:** board/export/ảnh có ngày và kết quả checklist người dùng. Không cần viết thêm hệ thống quản lý task trong app để đáp ứng yêu cầu này.

### 3.4. Đầu việc có điều kiện — tránh tự mở rộng phạm vi

- [ ] **SCOPE-01 — Làm rõ “quản lý đơn hàng toàn hệ thống”.** Nếu yêu cầu chỉ là xem/tìm/lọc/chi tiết thì phần đọc đã có; cần nghiệm thu và quyền đúng. Nếu bắt buộc điều hành đơn thì thêm đúng thao tác được yêu cầu (ví dụ hủy/cập nhật trạng thái), kèm state transition, audit và kiểm tra ảnh hưởng tiền/tồn kho. Chưa tự coi giỏ hàng/checkout/đấu giá đầy đủ là việc bắt buộc của phân hệ Admin.
- [ ] **SCOPE-02 — Làm rõ mức tích hợp thanh toán.** Nếu bài chấp nhận mock, phải ghi rõ payout/refund là mô phỏng và nghiệm thu tính nhất quán dữ liệu; nếu yêu cầu chuyển tiền thật, cần đầu việc tích hợp, đối soát, xác minh kết quả/idempotency riêng. Không đóng phần “hoàn tiền thật” chỉ vì DB có Refunded hoặc transaction ID là GUID.
- [ ] **SCOPE-03 — Chốt hình thức thống kê và quyền động.** Chốt lọc ngày/tháng/quý hay biểu đồ chuỗi tổng hợp, và role cố định hay Permissions tùy chỉnh. Chỉ triển khai theo phạm vi được yêu cầu; dù chọn phương án nào, nhãn UI và việc kiểm soát ở backend phải khớp mô tả.

### 3.5. Trình tự thực hiện đề xuất

| Đợt | Mục tiêu | Task chính | Điều kiện chuyển đợt |
|---|---|---|---|
| 1 | Bảo vệ quyền, danh tính và dữ liệu | TODO-01…06; phần chống trùng tiền của TODO-08 | Sai role/mạo danh/giả IP bị chặn; secret không rò log; nâng schema không mất dữ liệu; không xử lý tiền trùng trong ca kiểm tra. |
| 2 | Hoàn tất nghiệp vụ và số liệu | TODO-07…13; chốt SCOPE-01…03 | Luồng review/dispute/return/email hoạt động đủ bước; thống kê và lịch payout đúng; rate limit được kiểm chứng. |
| 3 | Pipeline và chứng minh vận hành | TODO-14…18 | CI/CD có run thật; security test đã kiểm lại; có số liệu scale/rollout/rollback và minh chứng quản lý nhóm/UI. |

Có thể làm TODO-14 và chuẩn bị kịch bản đo song song về mặt công việc nhóm, nhưng kết quả nghiệm thu cuối phải chạy trên bản đã sửa. Không cần đợi hoàn thiện giao diện mới bắt đầu sửa quyền hoặc chống mất dữ liệu.

## 4. Kết quả JMeter đã có trong repository

Theo README, các kết quả đo ngày 20–21/10/2026 ở commit `4983a90`, trên Compose **một instance API**, cùng máy chạy JMeter. Đã kiểm tra file statistics.json, không chạy lại JMeter trong lượt so sánh này.

| Nhóm | Bằng chứng được đọc | Ý nghĩa |
|---|---|---|
| Dispute detail 500 users | `tests/jmeter/results/baseline/04-dispute-detail-500u/report/statistics.json`: 46.109 mẫu GET detail, 0 lỗi, p95 khoảng 2.651 ms, throughput khoảng 153,84 req/s | Đã có số liệu thật trong repo và phát hiện bão hòa hiệu năng; không phải mọi API đều đáp ứng tải tốt. |
| IP whitelist | `results/security/10-security-ip-whitelist/report/statistics.json`: 5 mẫu, 1 lỗi assertion (20%) | Test tìm ra hành vi sai; lỗi không tự biến mất khi thêm test. |
| 2FA | `results/security/11-security-2fa/report/statistics.json`: 4 mẫu, 0 lỗi | Các ca đã chạy qua; không bao phủ hết vấn đề challenge mật khẩu/OTP hoặc enrollment. |
| RBAC | `results/security/12-security-rbac/report/statistics.json`: 16 mẫu, 3 lỗi assertion (18,75%) | Còn lỗi phân quyền. README nêu Monitor lọt vào Dispute, Support xem settlement và anonymous lọt vào Return API. |

Các file `results.jtl` và HTML report tương ứng đều có trong repo. Đầu việc W7-18/W7-19 về **tạo/chạy/lưu kết quả kiểm thử** đã có bằng chứng; sửa SEC-01…SEC-07 là đầu việc tiếp theo.

## 5. Trạng thái 18 đầu việc còn thiếu trong báo cáo trước

| # cũ | Đầu việc | Sau pull |
|---|---|---|
| 1 | Bảo vệ API admin | **Chưa giải quyết.** Products/Reviews/Return chưa áp policy đầy đủ; Disputes vẫn chỉ yêu cầu login. SensitiveLogs mới có policy SuperAdmin đúng phạm vi riêng của nó. |
| 2 | Đồng bộ quyền FE/BE | **Chưa giải quyết.** Các chỗ lệch role cũ chưa thay đổi. |
| 3 | AdminId từ phiên xác thực | **Chưa giải quyết.** ReviewMonitoringPage vẫn gửi adminId: 1; nhiều API vẫn tin ID từ body. |
| 4 | Hoàn thiện 2FA | **Chưa giải quyết.** Source Authentication không đổi; chưa gắn verify OTP với challenge đã xác minh mật khẩu, vẫn bật 2FA khi tạo QR. |
| 5 | IP allowlist / trusted proxy | **Chưa giải quyết.** InternalIpMiddleware không đổi; tài liệu security mới xác nhận lỗi prefix 10.* và trusted forwarded headers. |
| 6 | Log nhạy cảm riêng + mã hóa | **Tiến triển một phần.** Có bảng/mã hóa/UI/giải mã nhưng chỉ ghi log mẫu, LoggingBehaviour vẫn log nguyên request. |
| 7 | Biên ngày/timezone thống kê | **Chưa giải quyết.** Các query Stats và hook dashboard không đổi. |
| 8 | Thống nhất doanh thu và hiển thị user growth | **Chưa giải quyết.** Dashboard/Stats không đổi. |
| 9 | Review + Feedback | **Đã giải quyết phần thiếu màn/API giám sát Feedback.** Chưa giải quyết cập nhật dữ liệu tổng hợp, xác minh đã mua hàng, ràng buộc người thao tác và quyền API. |
| 10 | Điều phối dispute / refund hợp lệ | **Chưa giải quyết nghiệp vụ.** Có thêm test assign/resolve, không có thay đổi handler nghiệp vụ tương ứng. |
| 11 | Nhận hàng trả → hoàn tiền | **Chưa giải quyết nghiệp vụ.** Thêm test hoàn trả không bổ sung bước còn thiếu; folder ReturRequests không đổi. |
| 12 | Worker gửi email Scheduled/Pending | **Chưa giải quyết.** Broadcast/EmailService chưa có thay đổi giải quyết phần này. |
| 13 | Thao tác điều hành đơn hàng nếu bắt buộc | **Chưa thay đổi.** Vẫn chủ yếu danh sách/chi tiết. |
| 14 | Sửa CI --no-build / mở rộng kiểm thử | **Đã sửa lỗi --no-build và thêm functional test vào CI.** Application unit test tăng 45; chưa thấy frontend check trong CI, Domain failure vẫn không chặn pipeline. |
| 15 | CD đợi CI rồi deploy/verify | **Chưa giải quyết.** cd.yml không đổi, vẫn needs: [], build/push image; script rollout là thao tác riêng. |
| 16 | Job nhiều replica / chống xử lý trùng | **Tiến triển một phần về shutdown và phục hồi.** Chưa có distributed lock/atomic claim; không đóng task concurrency. |
| 17 | Payout dùng giờ/tần suất cấu hình | **Chưa giải quyết.** Background service vẫn tính 02:00 UTC hằng ngày; thay đổi chỉ ở xử lý cancellation. |
| 18 | Jira, JMeter, minh chứng scale/rollout | **JMeter đã có.** Chưa thấy Jira, kết quả tải so sánh 1 vs 2 instance hay log đo zero downtime xuyên suốt rollout. |

Kết quả tương ứng Requirement.txt: A6 và A13 có tiến triển rõ; B1/B2/B3/B6 có triển khai thêm nhưng chưa nghiệm thu trọn vẹn; B5 từ “chưa thấy minh chứng” chuyển thành **đã có kịch bản và kết quả kiểm thử tải/an ninh**. B4 Jira vẫn chưa có bằng chứng mới. Các yêu cầu còn lại không có thay đổi đủ để kết luận đã đóng.

## 6. Chi tiết bằng chứng rủi ro của các task mới

### Cần sửa

1. **Sensitive logs chưa ghi nhận thao tác thật.**
   - Hiện chỉ `SensitiveDataLogsSeeder` gọi Encrypt rồi AddRange vào SensitiveDataLogs; query/API chỉ đọc/giải mã. Thực hiện payout, đổi role hoặc cập nhật thông tin nhạy cảm chưa tạo bản ghi riêng ở đây.
   - Cách hoàn thiện tối thiểu: ghi bản mã từ các handler nghiệp vụ liên quan và ngừng ghi secret vào LoggingBehaviour. Nếu bỏ qua, màn hình đẹp và dữ liệu seed không chứng minh log nhạy cảm thật đã được bảo vệ.

2. **Seeder không dùng cùng cấu hình khóa với API giải mã.**
   - `SensitiveDataLogsSeeder.cs:25` tự tạo AesEncryptionService với ConfigurationBuilder rỗng. DI đăng ký AesEncryptionService đọc Security:EncryptionKey; bản seed luôn dùng khóa fallback còn API có thể dùng khóa môi trường.
   - Trường hợp lỗi: cấu hình EncryptionKey thật rồi seed DB mới; bấm decrypt sẽ báo decryption/integrity error. Dùng cùng service/cấu hình cho cả ghi và đọc, không dựa vào fallback khóa cứng trong source.

3. **Schema log mới được dựng bằng seeder, có nhánh DROP TABLE.**
   - Không có migration SensitiveDataLogs trong folder Migrations. Seeder dùng raw SQL tạo bảng, và xóa bảng nếu có nhưng thiếu CreatedAt.
   - Trường hợp lỗi: DB có bảng log cũ với dữ liệu nhưng schema thiếu CreatedAt; startup sẽ xóa toàn bộ log. Chuyển thay đổi schema sang migration giữ dữ liệu; không dùng seed để xóa lịch sử audit. Functional tests dùng EnsureCreated nên không kiểm chứng đường nâng cấp schema này.

4. **Payout cũ >45 giây chưa có cơ chế nhận xử lý độc quyền.**
   - ResumeStaleProcessingAsync tải cùng các dòng Processing rồi gọi FinalizePayoutAsync. Hai API có thể đồng thời nhận cùng một dòng; AnyAsync rồi SaveChanges của luồng payout mới cũng chưa trở thành thao tác nhận việc nguyên tử.
   - Trường hợp lỗi: hai worker/API gọi engine cùng lúc khi có payout cũ; cả hai đều gọi gateway và ghi hạch toán. Cần atomic claim/khóa phù hợp và kiểm thử SQL nhiều context. Mốc tuổi 45s cũng chưa chứng minh owner cũ đã chết.

### Cần hoàn thiện phần kiểm chứng

5. **Script rate limit chưa chứng minh quy tắc “request thứ 6 bị app chặn”.**
   - `scripts/test-rate-limit.ps1` gửi userName trong khi LoginCommand nhận Email. Qua round-robin 2 API, 6 request có thể chia 3/3; limiter app lưu theo process nên chưa chạm 5 ở một instance. Script chỉ in kết quả, không fail nếu kỳ vọng sai.
   - Gửi đúng email; kiểm thử app limiter ở một instance hoặc xác định rõ phạm vi từng instance; thêm assert/exit code. Đánh giá gateway theo IP riêng. Nếu bỏ qua, script có thể in kết luận thành công dù chưa kiểm tra được điều cần chứng minh.

6. **Các phần mới chưa có test trực tiếp cho rủi ro mới.**
   - 45 test bổ sung rất hữu ích, nhưng chưa thấy test AES/seeder với khóa cấu hình thật, test Feedback API, test tiếp tục payout stale, hay test concurrency/restart. Test Payout_AlreadyProcessing chỉ kiểm tra một lần chạy với một dòng Processing mới.
   - Ưu tiên test khóa ghi/đọc giống nhau, thao tác thật tạo log, và hai worker cùng nhận một payout cũ; kiểm thử SignalR negotiate/reconnect thực tế qua 2 API.

## 7. Kiểm tra đã thực hiện ở bản 18bf623 và giới hạn kết luận

| Kiểm tra | Kết quả mới |
|---|---|
| Backend Release build, --no-restore | Thành công: 0 warning, 0 error. |
| Application unit tests, --no-restore, Release | **73 passed, 0 failed, 0 skipped**, tăng từ 28 ở bản trước. |
| Functional test project Release build, --no-restore | Thành công: 0 warning, 0 error. |
| Frontend `npm.cmd run build` | Thành công, compiled with warnings; main bundle khoảng 655,61 kB gzip. Cảnh báo key trùng endpoints.js ở bản trước đã hết; vẫn còn cảnh báo hooks, biến không dùng và accessibility. |
| Docker/functional test runtime | Không chạy: lệnh docker không có trong PATH của terminal. Không khởi động ứng dụng hay migrate/seed DB thật. |
| JMeter | Đọc plan, README, JTL có mặt và statistics.json đã lưu; không chạy lại. |
| K8s/Nginx/CD/Jira | Đọc diff/cấu hình/script; chưa chạy cluster, pipeline hoặc truy cập công cụ quản lý team. |

Kết luận: có thể ghi nhận hoàn tất các đầu việc cụ thể **sửa CI unit-test, thêm 45 unit test, thêm bộ functional test, thêm màn/API Feedback, thêm cấu hình Nginx hai instance, thêm JMeter plan/report**. Chưa nên đóng toàn bộ task **log nhạy cảm, rate limiting ở app, chống trùng payout, bảo mật Admin và zero downtime**.

Chỉ thêm tài liệu so sánh, không sửa code. Rủi ro ưu tiên: API chưa bảo vệ đủ và log password/OTP cũ còn nguyên; seeder log mới có đường xóa bảng; chạy nhiều API vẫn chưa bảo đảm payout được xử lý đúng một lần.
