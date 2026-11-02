# W8-03 — Báo cáo load test cụm: 1 vs 2 vs 3 replica API

## Cấu hình

- **Cụm:** `docker-compose.yml` + `docker-compose.loadtest.yml`: SQL Server 2022, Redis, Nginx round-robin, 1/2/3 replica API.
- **Mỗi replica:** giới hạn 1 CPU / 768 MB, để số replica là biến duy nhất thay đổi.
- **Nginx:** dùng config load test, không có `limit_req`, để đo năng lực API chứ không đo rate limit.
- **Tải (`config/cluster.properties`):** 200 thread, ramp-up 20 s, chạy 120 s, think time 0–200 ms (closed-loop). Token đăng nhập 1 lần trong setUp Thread Group.
- **Kịch bản giống nhau cho cả 3 cấu hình:**
  - 02: Dashboard/Stats, 5 API thống kê.
  - 03: Users/Orders, list phân trang.
  - 04: Dispute detail theo id.
- **Môi trường:** JMeter, Docker và DB chạy chung 1 máy Windows (Docker Desktop/WSL2, 7.5 GB RAM).

## Bảng so sánh

| Kịch bản | Replica | Throughput (req/s) | So với 1 replica | Mean (ms) | p95 (ms) | Error % | HTML report |
|---|---:|---:|---:|---:|---:|---:|---|
| 02 Dashboard/Stats | 1 | 210.5 | x1.00 | 763 | 1809 | 0 | [report](r1/02-dashboard-stats-20261031-122904/report/index.html) |
| 02 Dashboard/Stats | 2 | 278.0 | x1.32 | 549 | 2008 | 0 | [report](r2/02-dashboard-stats-20261101-005646/report/index.html) |
| 02 Dashboard/Stats | 3 | 325.1 | **x1.54** | 455 | 1948 | 0 | [report](r3/02-dashboard-stats-20261101-010712/report/index.html) |
| 03 Users/Orders paging | 1 | 196.9 | x1.00 | 823 | 1623 | 0 | [report](r1/03-users-orders-paging-20261031-123133/report/index.html) |
| 03 Users/Orders paging | 2 | 294.6 | x1.50 | 513 | 1762 | 0 | [report](r2/03-users-orders-paging-20261101-005918/report/index.html) |
| 03 Users/Orders paging | 3 | 368.8 | **x1.87** | 383 | 1613 | 0 | [report](r3/03-users-orders-paging-20261101-010939/report/index.html) |
| 04 Dispute detail | 1 | 100.5 | x1.00 | 1708 | 2965 | 0 | [report](r1/04-dispute-detail-20261031-123356/report/index.html) |
| 04 Dispute detail | 2 | 122.1 | x1.21 | 1384 | 3493 | 0 | [report](r2/04-dispute-detail-20261101-010143/report/index.html) |
| 04 Dispute detail | 3 | 153.8 | **x1.53** | 1076 | 3717 | 0 | [report](r3/04-dispute-detail-20261101-011204/report/index.html) |

Số liệu đầy đủ (gồm p99 và số sample) ở `summary.md` / `summary.csv`.

## Nhận xét

1. **Throughput tăng theo số replica.** Lên 3 replica, throughput tăng **x1.5–x1.9**. Thời gian phản hồi trung bình giảm **37–53%**.
2. **Không có lỗi.** Error rate 0% ở cả 9 lần chạy (~250.000 request). Nginx chia tải đều, không replica nào bị quá tải.
3. **Không tăng tuyến tính (x3)** vì các replica dùng chung:
   - 1 SQL Server, nên DB là nút cổ chai chung;
   - CPU của cùng 1 máy với JMeter.

   Kịch bản 03 (query đơn giản) scale tốt nhất. Kịch bản 04 và 02 (query nặng / nhiều aggregate) scale kém hơn.
4. **p95 gần như không giảm.** Kịch bản 04 còn tăng từ 2.97 s lên 3.72 s. Đây là test closed-loop: cụm xử lý nhanh hơn thì 200 thread gửi nhiều request hơn, nên hàng đợi chuyển xuống DB. Độ trễ phần đuôi vì vậy do DB quyết định, không phải do API.

## Kết luận (cho phần bảo vệ)

> Hệ thống scale ngang đúng thiết kế. Chỉ cần thêm replica API sau Nginx là năng lực xử lý tăng: 3 replica phục vụ được **1.5–1.9 lần** số request/giây so với 1 replica, với cùng 200 người dùng đồng thời. Thời gian phản hồi trung bình giảm tới ~50%, và **không phát sinh lỗi** (0%). Tăng không tuyến tính vì mọi replica dùng chung một SQL Server, nên DB là giới hạn tiếp theo. Hướng cải thiện: tối ưu query xem chi tiết Dispute (p95 ~3–3.7 s), và cache số liệu Dashboard bằng Redis đã có sẵn trong cụm.

## Cách chạy lại

```powershell
cd tests\jmeter
.\scripts\run-cluster.ps1                 # 1, 2, 3 replica x plan 02/03/04
```
