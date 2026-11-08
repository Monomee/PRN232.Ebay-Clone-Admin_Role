# 05. Thống kê hệ thống theo ngày, tháng, quý (doanh thu, đơn hàng, người dùng mới)

## Mô tả trong dự án

| Lớp | Vị trí |
|---|---|
| Giao diện | Thanh chọn khoảng thời gian trên `/dashboard`: **Today / Weekly / Monthly / Quarterly / All Time / Custom** (`hooks/useDashboardData.js` → `getPresetRange`) |
| API (policy `ViewReports`) | `GET /api/Stats/reports/revenue?StartDate=&EndDate=`, `GET /api/Stats/reports/users`, `GET /api/Stats/reports/orders` |
| Query | `Application/Stats/Queries/` (`GetRevenueStatsQuery`, ...). Doanh thu sàn = tổng giao dịch `FeeDeduction`, gom theo ngày |
| Xuất báo cáo | Nút Excel (`xlsx`) và PDF (`jspdf`) ở `DashboardActions.jsx` → `services/exportService.js` |

Cách tính khoảng thời gian (theo lịch, không phải "30 ngày gần nhất"):
- **Quarterly:** từ ngày đầu quý hiện tại đến hôm nay.
- **Monthly:** từ ngày 1 của tháng đến hôm nay.
- **Custom:** chọn ngày bắt đầu và kết thúc. Nếu ngày kết thúc nhỏ hơn ngày bắt đầu, server trả lỗi.

## Để làm gì

- Ban quản lý cần số theo kỳ để báo cáo: doanh thu tháng này so với tháng trước, quý này có bao nhiêu user mới.
- Có nút xuất Excel/PDF để gửi báo cáo mà không cần chụp màn hình.

## Kịch bản demo

1. Đăng nhập **Monitor** (`monitor@ebay.local`). Vai trò này được tạo ra cho việc xem báo cáo.
2. Trên dashboard, lần lượt bấm **Today → Monthly → Quarterly → All Time**. Mỗi lần bấm:
   - tiêu đề khoảng ngày đổi theo, ví dụ "Oct 1, 2026 – Nov 8, 2026";
   - biểu đồ doanh thu (`RevenueChart`) và phân bố đơn (`OrderDistribution`) vẽ lại.
3. Bấm **Custom**, chọn một khoảng tùy ý, ví dụ 1 tuần.
4. Mở DevTools > Network và chỉ vào request `reports/revenue?StartDate=...&EndDate=...`: lọc ngày được làm ở server.
5. Bấm nút **Excel**: tải file `Dashboard_Report_<ngày>.xlsx`. Mở cho lớp xem các sheet số liệu.
6. Bấm nút **PDF**: tải bản chụp dashboard dạng PDF.

## Câu hỏi hay gặp

- **Sao Support không thấy biểu đồ?** Support có `ViewDashboard` nhưng không có `ViewReports`. Báo cáo doanh thu chỉ dành cho Monitor và SuperAdmin (xem file 11).
