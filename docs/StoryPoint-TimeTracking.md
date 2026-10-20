# Story Point & Time Tracking – Cách track và đánh giá

Tài liệu dùng chung cho cả nhóm, áp dụng cho folder ClickUp **Sprints (Tuần 1–9)**.

## 0. Dữ liệu nằm ở đâu trên ClickUp

| Thông tin | Nơi ghi | Ghi chú |
|---|---|---|
| Story Point (độ khó) | Tag `sp-1`, `sp-2`, `sp-3`, `sp-5`, `sp-8` | Chỉ gắn cho task lá (task 1 người hoặc subtask). Task cha không gắn để tránh cộng trùng |
| Giờ ước lượng | Time Estimate của task | Task cha để 0, ClickUp tự cộng từ subtask |
| Giờ làm thật | Time Tracking (nút đồng hồ) | Từng người tự bấm |
| Trạng thái | Status `to do` / `in progress` / `complete` | |

Mỗi task chỉ **1 người nhận**. Việc nhiều người làm thì tách thành subtask, mỗi người một subtask.

## 1. Thang Story Point (Fibonacci)

SP đo **độ khó + độ chưa chắc chắn**, không phải số giờ.

| SP | Ý nghĩa | Ví dụ trong dự án |
|---|---|---|
| 1 | Việc nhỏ, đã biết rõ cách làm | Sửa config, dọn code, họp |
| 2 | Đơn giản, chạm 1 chỗ | Lập ClickUp, cập nhật tài liệu |
| 3 | Bình thường, vài bước | docker-compose local, test case 1 phân hệ |
| 5 | Phức tạp vừa, chạm nhiều layer | API Users/Products, UI 3 màn hình |
| 8 | Khó / có phần chưa biết | 2FA + IP nội bộ, K8s end-to-end, mã hoá log nhạy cảm |
| 13 | Quá lớn | **Không giao**, phải tách nhỏ trước |

## 2. Trong tuần – việc của mỗi thành viên

1. Bắt đầu làm task → bấm **Track time** (biểu tượng đồng hồ trong task); nghỉ thì bấm dừng. Quên bấm thì vào task thêm giờ bằng tay.
2. Làm xong → chuyển status sang **complete** ngay, không dồn đến cuối tuần.
3. Thấy task khó hơn dự kiến → **comment vào task ngay**, không chờ đến hạn.

## 3. Cuối tuần – PM đọc 3 chỉ số cho từng người

| Chỉ số | Cách tính | Cho biết gì |
|---|---|---|
| **Velocity** | Tổng SP các task **complete** trong tuần | Mỗi tuần người đó làm xong được bao nhiêu khối lượng |
| **Tỉ lệ đúng hạn** | Số task xong trước due date / số task được giao | Có giữ được cam kết không |
| **Hệ số ước lượng** | Giờ track thật / giờ ước lượng | Ước lượng có sát thực tế không |

### Cách đọc

| Tình huống | Ý nghĩa | Hành động |
|---|---|---|
| Velocity đạt, hệ số khoảng 1 | Làm tốt | Giữ mức giao việc |
| Velocity đạt, hệ số > 1,3 | Kịp nhưng phải làm thêm giờ | Tuần sau giảm tải |
| Velocity thấp, hệ số > 1,3 | Task bị đánh giá quá dễ, hoặc người làm đang vướng | Hỏi nguyên nhân trước khi giao thêm |
| Velocity thấp, hệ số < 1 | Còn thời gian trống, hoặc quên track giờ | Kiểm tra lại việc track giờ, rồi giao thêm |
| Hay trễ đúng loại task `sp-8` | Task quá lớn so với người đó | Tách thành 2 task 3–5 SP |

## 4. Đầu tuần – xếp việc tuần sau

1. Lấy **velocity trung bình 2–3 tuần gần nhất** của từng người, đó là sức chứa của họ.
2. Giao tổng SP **không vượt khoảng 90% sức chứa**, chừa chỗ cho bug và việc phát sinh.
3. Task `sp-8` ưu tiên giao cho người có hệ số ước lượng ổn định nhất với loại việc đó (BE / FE / Test / DevOps).
4. Không xếp 2 task `sp-8` cho cùng một người trong cùng một tuần.

**Ví dụ:** 2 tuần qua Cường hoàn thành 12 và 14 SP, nên trung bình 13 SP. Hệ số ước lượng của Cường là 1,4. Tuần tới chỉ nên giao Cường khoảng 10–11 SP, và tránh giao loại task mà Cường vừa bị vượt giờ.

## 5. Xem trên ClickUp

- Mở List của tuần → **Group by: Assignee** → lọc theo **Tags** để thấy mỗi người có bao nhiêu task `sp-N`.
- ClickUp (gói Free) **không tự cộng SP từ tag**: tổng velocity phải cộng tay, hoặc nhờ Claude đọc ClickUp và lập bảng tổng kết tuần.
- Giờ track thật xem ở **Timesheet** (thanh bên trái), lọc theo người và theo tuần.

## 6. Lưu ý riêng cho dự án này

- **Tuần 1–6 không dùng làm số liệu được.** Đó là kế hoạch ghi bù, không có giờ track thật.
- **Tuần 7 (19–25/10) là tuần đầu tiên có dữ liệu thật**, nên cả nhóm phải bấm Track time ngay từ đầu tuần.
- Velocity cuối tuần 7 dùng để chỉnh tải tuần 8. Tuần 9 chủ yếu là demo, nên số liệu chỉ còn giá trị để rút kinh nghiệm.

## 7. Tổng kết SP – cập nhật 20/10/2026 (lần 2)

Nguồn: ClickUp folder **Sprints (Tuần 1–9)**, 135 task lá, tổng 421 SP. Các ô ghi theo dạng *SP hoàn thành / SP được giao*.

| Tuần | Hoàng | Mạnh | Dương | Cường | Tùng | **Cả nhóm** |
|---|---|---|---|---|---|---|
| 1 | 9/9 | 10/10 | 8/8 | 6/6 | 6/6 | **39/39** |
| 2 | 9/9 | 11/11 | 8/8 | 10/10 | 8/8 | **46/46** |
| 3 | 15/15 | 5/5 | 5/5 | 8/8 | 5/5 | **38/38** |
| 4 | 13/13 | 10/10 | 8/8 | 3/3 | 6/6 | **40/40** |
| 5 | 8/8 | 10/10 | 10/10 | 5/5 | 5/5 | **38/38** |
| 6 | 5/7 | 5/7 | 5/7 | 7/9 | 7/9 | **29/39** |
| 7 (đang chạy) | 0/17 | 0/19 | 0/17 | 1/18 | 0/17 | **1/88** |
| 8 | 0/15 | 0/15 | 0/14 | 0/13 | 0/10 | **0/67** |
| 9 | 0/6 | 0/6 | 0/6 | 0/4 | 0/4 | **0/26** |
| **Tổng** | **59/99** | **51/93** | **44/83** | **40/76** | **37/70** | **231/421** |

### Velocity và tải tuần 7

| | Hoàng | Mạnh | Dương | Cường | Tùng |
|---|---|---|---|---|---|
| Velocity TB tuần 4–6 (SP) | 8,7 | 8,3 | 7,7 | 5,0 | 6,0 |
| SP giao tuần 7 | 17 | 19 | 17 | 18 | 17 |
| Gấp bao nhiêu lần velocity | ~2,0× | ~2,3× | ~2,2× | ~3,6× | ~2,8× |
| SP tuần 1–6 chưa complete | 2 | 2 | 2 | 2 | 2 |

### Nhận xét

1. **Chưa có giờ track thật**: chỉ có 4 time entry ~10 giây (bấm thử trên W7-01, W7-03, W7-05, W7-09), nên chưa tính được hệ số ước lượng. Tỉ lệ đúng hạn tuần 1–6 không có ý nghĩa vì các task đó được đánh complete bù vào 18–19/10.
2. Velocity tuần 4–6 chỉ để tham khảo, xem mục 6.
3. Tuần 1–3 đã đóng hết (+102 SP so với bản trước). Tuần 1–6 chỉ còn **10 SP in progress**: W6-04 (Hoàng, 2 SP) và W6-05 (Mạnh, Dương, Cường, Tùng, mỗi người 2 SP). Xong thì đóng nốt, hoặc dời sang tuần 7 nếu còn làm.
4. **Tuần 7 quá tải:** 88 SP, trong khi các tuần trước nhóm làm khoảng 40 SP/tuần. Theo mục 4 (giao không quá 90% sức chứa), mỗi người chỉ nên nhận khoảng 7–8 SP.
   - Căng nhất là Cường (có W7-15 sp-8) và Mạnh (có W7-05 sp-8, hạn 20/10, đang chặn W7-02, W7-15, W8-16).
   - Đề xuất: dời các task không nằm trên đường găng (ví dụ W7-13, W7-17, W7-21) sang tuần 8, hoặc dùng velocity thật cuối tuần 7 để chỉnh tải tuần 8.
