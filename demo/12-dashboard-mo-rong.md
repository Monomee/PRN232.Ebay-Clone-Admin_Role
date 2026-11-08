# 12. 📈 Có khả năng mở rộng dashboard để theo dõi nhiều loại thống kê hơn

## Mô tả trong dự án

Dashboard được ghép từ các **widget độc lập** trong `ClientApp/src/components/dashboard/`. Mỗi widget nhận dữ liệu qua props, không phụ thuộc nhau:

| Widget | Dữ liệu | Hiện khi có quyền |
|---|---|---|
| `MetricTiles` | Số user, sản phẩm, đơn hôm nay | `ViewDashboard` |
| `ActionCenter` | Việc chờ xử lý | `ViewDashboard` |
| `RevenueChart` | Doanh thu theo ngày | `ViewReports` |
| `OrderDistribution` | Phân bố đơn theo trạng thái | `ViewReports` |
| `PlatformFeeWidget` | Phí sàn | quyền tài chính |
| `SellerLevelConfigWidget` | Tiêu chí cấp seller | quyền tương ứng |

Phía backend:
- Mỗi loại thống kê là một **MediatR query** riêng trong `Application/Stats/Queries/`.
- Mỗi query được map thành một endpoint riêng trong `Web/Endpoints/Stats.cs`.
- Hook `useDashboardData.js` lo khoảng thời gian (preset) chung cho mọi widget.

Thêm một loại thống kê mới gồm **4 bước**, không sửa code cũ:

1. `Application/Stats/Queries/GetXxxStatsQuery.cs`: record query, DTO, handler.
2. `Web/Endpoints/Stats.cs`: `groupBuilder.MapGet(GetXxxStats, "reports/xxx").RequireAuthorization(Policies.ViewReports);`
3. `components/dashboard/XxxWidget.jsx`: nhận `stats.xxx`, vẽ bằng thư viện biểu đồ đang dùng.
4. `DashboardPage.jsx`: thêm `{canViewReports && <XxxWidget data={stats.xxx} />}`. Hook tự truyền khoảng ngày đang chọn.

Ngoài ra:
- **Xuất Excel** (`exportService.js`) lấy dữ liệu từ object `stats`, nên widget mới có thể thêm sheet riêng.
- Custom role (file 11) quyết định **ai** thấy widget mới, không cần code phân quyền thêm.

## Để làm gì

- Nhu cầu thống kê luôn tăng theo thời gian: tỉ lệ hoàn trả, top seller, review xấu theo danh mục...
- Kiến trúc widget giúp thêm chỉ số mới nhanh và không ảnh hưởng các widget đang chạy.

## Kịch bản demo

1. Mở `DashboardPage.jsx` trên máy chiếu. Chỉ phần render: mỗi dòng là một widget, kèm điều kiện quyền.
2. Mở `components/dashboard/` để cho thấy mỗi widget là một file nhỏ.
3. Mở `Web/Endpoints/Stats.cs`: các endpoint `reports/revenue`, `reports/users`, `reports/orders` cùng một mẫu.
4. **Demo trực tiếp (nếu có 3 phút): thêm widget "Dispute đang mở"** mà không viết API mới:
   - dữ liệu `metrics.openDisputesCount` đã có sẵn trong `/api/Dashboard/metrics`;
   - tạo `OpenDisputesWidget.jsx`:
     ```jsx
     export const OpenDisputesWidget = ({ metrics }) => (
       <div className="card p-3 shadow-sm"><div className="text-muted small">Open disputes</div>
         <div className="fs-2 fw-bold">{metrics?.openDisputesCount ?? 0}</div></div>);
     ```
   - thêm `<OpenDisputesWidget metrics={metrics} />` vào `DashboardPage.jsx`;
   - React tự reload (hot reload) và widget mới hiện ngay.
   - **Nhớ `git checkout` để bỏ thay đổi sau buổi demo.**
5. Đổi preset Monthly → Quarterly: mọi widget thống kê cùng đổi theo, vì dùng chung hook.
