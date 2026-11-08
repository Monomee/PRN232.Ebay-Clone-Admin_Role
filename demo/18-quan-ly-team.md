# 18. Quản lý team: Jira

> Nhóm dùng **ClickUp** (tuần 1–9), sau đó chuyển nguồn chính sang **Google Sheet** "eBay Admin Clone – Quản lý dự án (Tuần 1–9)" từ ngày 06/11. Không dùng Jira. ClickUp có đủ các chức năng Jira cần cho đồ án: sprint, assignee, story point, dependency, time tracking. Gói Free của ClickUp không giới hạn số thành viên.

## Mô tả trong dự án

| Hạng mục | Cách làm |
|---|---|
| Sprint | Folder **"Sprints (Tuần 1–9)"**, mỗi tuần một list. Tuần 7: tích hợp, feature freeze 25/10. Tuần 8: test và polish, code freeze, tag v1.0 ngày 01/11. Tuần 9: dự phòng và bảo vệ |
| Task | Mỗi task có người nhận, ngày, ước lượng, độ ưu tiên, **DoD** (Definition of Done), phụ thuộc. Task nhiều người được tách thành subtask cho từng người |
| Story point | Tag `sp-1/2/3/5/8` cho từng task lá (gói Free giới hạn số lần ghi custom field). Bảng tổng hợp SP trong `docs/StoryPoint-TimeTracking.md` |
| Liên kết code | Tên nhánh và commit mang mã task, ví dụ `W7-09`. Mở ClickUp là biết task nằm ở commit nào |
| Phân vai | BE + DevOps (codebase, CI/CD); BE + DevOps (K8s, LB, Nginx, rate limit); FE; BE + Test (JMeter, zero downtime). Chi tiết ở sheet **Team** |
| Bug | Sheet **Bugs** (58 bug, có trạng thái) |
| Đối chiếu | `docs/ClickUp-Task-Code-Audit-2026-10-28.md` so trạng thái task với bằng chứng trong code |

## Để làm gì

- 5 người làm song song cần biết **ai làm gì, đến khi nào, việc nào đang chặn việc nào**. Ví dụ: chạy K8s chặn cả CD lên K8s lẫn zero downtime.
- Story point và velocity để biết tuần nào ôm quá sức (tuần 7: 88 SP so với khoảng 40 SP mỗi tuần) và điều chỉnh sớm.

## Kịch bản demo

1. Mở ClickUp, vào folder **Sprints (Tuần 1–9)**. Dùng **Board view** để thấy các cột trạng thái.
2. Mở một task, ví dụ W7-19 (security test JMeter). Chỉ vào:
   - người nhận, hạn, story point (tag);
   - DoD;
   - **dependency** (task bị chặn bởi task nào).
3. Mở **Google Sheet** quản lý dự án:
   - sheet **Dashboard**: tiến độ theo tuần và theo người;
   - sheet **Tasks**: 1 dòng cho mỗi cặp người–task;
   - sheet **Bugs**.
4. Đối chiếu với GitHub:
   - mở danh sách PR (`gh pr list --state all -L 10`);
   - tên nhánh và commit khớp mã task;
   - PR có CI xanh (file 17).
5. Mở `docs/StoryPoint-TimeTracking.md`: bảng SP đã xong và còn lại theo tuần.

## Câu hỏi hay gặp

- **Sao không dùng Jira?** Yêu cầu ghi Jira như một ví dụ công cụ. ClickUp tương đương và nhóm đã quen. Nếu thầy bắt buộc Jira: import CSV từ sheet Tasks vào Jira mất khoảng 15 phút.
