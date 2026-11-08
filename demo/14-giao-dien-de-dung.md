# 14. 🧩 Giao diện dễ sử dụng cho quản trị viên không chuyên kỹ thuật

## Mô tả trong dự án

| Nguyên tắc | Cách làm trong dự án |
|---|---|
| Chỉ thấy cái mình dùng được | Sidebar (`NavMenu.js`) và widget dashboard ẩn theo quyền (file 11). Monitor không bị rối bởi các nút không bấm được |
| Việc cần làm hiện ngay | **Action Center** trên dashboard; tab **Risk Review** gom tài khoản chờ duyệt, điểm cao lên đầu (`/users?tab=RiskReview`) |
| Không cần biết SQL hay API | Mọi thao tác có nút: Approve / Reject / Ban / Unban / Lift restrictions; Hide / Restore / Delete & Warn; Assign to me / Resolve; Freeze; Block / Lift IP |
| Giải thích bằng lời | Card **Account Linking** ghi lý do bằng lời (*"Trùng thiết bị với tài khoản bị khóa"*), không chỉ hiện con số |
| Phản hồi rõ ràng | `ToastMessage` sau mỗi thao tác. Lỗi validation hiện **đúng câu của server** (`apiRequest` đọc `errors` của ValidationProblemDetails), không chung chung |
| Realtime, không phải F5 | SignalR: dispute, thông báo, return request, ví seller tự cập nhật |
| Xuất báo cáo một nút | Excel và PDF trên dashboard |
| Bộ lọc nhanh | Preset Today / Weekly / Monthly / Quarterly; tab trạng thái; ô tìm kiếm; phân trang |
| Hết phiên tự xử lý | API trả 401 thì tự chuyển về `/login` (`services/sessionGuard.js`), không hiện lỗi khó hiểu |
| Thao tác nguy hiểm có bước xác nhận | Ban cần nhập lý do; Resolve dispute cần ghi chú; Block IP giới hạn dải rộng nhất `/16` để gõ nhầm không chặn cả nhà mạng |
| Có công cụ tạo dữ liệu mẫu | Trang **Mock** (*Developer Simulation Toolbox*) để huấn luyện admin mới trên dữ liệu giả |

## Để làm gì

- Người dùng trang admin thường là nhân viên CSKH, vận hành, kế toán, không phải lập trình viên.
- Giao diện tốt làm giảm sai sót (ban nhầm người, hoàn tiền nhầm) và giảm thời gian đào tạo.

## Kịch bản demo (kể như một ngày làm việc của nhân viên Support)

1. Đăng nhập Support. Dashboard cho biết *"3 tài khoản chờ duyệt, 2 dispute mở"*.
2. Bấm vào ô **tài khoản chờ duyệt** → sang tab **Risk Review**. Danh sách đã xếp sẵn theo mức rủi ro.
3. Mở tài khoản đầu tiên. Card Account Linking ghi bằng lời *vì sao* tài khoản đáng ngờ. Bấm **Reject**, toast báo thành công.
4. Quay lại, mở một dispute, bấm **Assign to me**, chọn bên thắng, bấm **Resolve**. Không phải gõ câu lệnh nào.
5. Cố tình nhập sai, ví dụ số tiền hoàn âm: thông báo lỗi bằng câu dễ hiểu ngay trên form.
6. Chỉ vào sidebar và so với Monitor: mỗi người chỉ thấy những gì liên quan tới mình.
7. Bấm Excel trên dashboard để tải báo cáo gửi sếp.

## Hạn chế (nói thật nếu bị hỏi)

- Giao diện chủ yếu bằng tiếng Anh, thông báo lỗi nghiệp vụ bằng tiếng Việt. Chưa làm đa ngôn ngữ (i18n).
- Ngày giờ hiển thị giờ UTC như giờ địa phương. Đây là lỗi có từ trước (`docs/Security-Hardening-2026-11.md`, mục 2, ghi chú 5).
