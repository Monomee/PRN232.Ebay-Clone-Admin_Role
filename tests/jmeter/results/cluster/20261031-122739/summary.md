# W8-03 — So sánh load test theo số replica

Thư mục: `20261031-122739` — mỗi replica giới hạn 1 CPU / 768 MB, Nginx round-robin, không rate limit.

| Kịch bản | Replica | Samples | Throughput (req/s) | So với 1 replica | Mean (ms) | p95 (ms) | p99 (ms) | Error % | HTML report |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 02-dashboard-stats | 1 | 25538 | 210.5 | x1.00 | 763 | 1809 | 2101 | 0 | [index.html](r1/02-dashboard-stats-20261031-122904/report/index.html) |
| 02-dashboard-stats | 2 | 33844 | 278 | x1.32 | 549 | 2008 | 2753 | 0 | [index.html](r2/02-dashboard-stats-20261101-005646/report/index.html) |
| 02-dashboard-stats | 3 | 39518 | 325.1 | x1.54 | 455 | 1948 | 2795 | 0 | [index.html](r3/02-dashboard-stats-20261101-010712/report/index.html) |
| 03-users-orders-paging | 1 | 23856 | 196.9 | x1.00 | 823 | 1623 | 2263 | 0 | [index.html](r1/03-users-orders-paging-20261031-123133/report/index.html) |
| 03-users-orders-paging | 2 | 35835 | 294.6 | x1.50 | 513 | 1762 | 2423 | 0 | [index.html](r2/03-users-orders-paging-20261101-005918/report/index.html) |
| 03-users-orders-paging | 3 | 45056 | 368.8 | x1.87 | 383 | 1613 | 2432 | 0 | [index.html](r3/03-users-orders-paging-20261101-010939/report/index.html) |
| 04-dispute-detail | 1 | 12197 | 100.5 | x1.00 | 1708 | 2965 | 3609 | 0 | [index.html](r1/04-dispute-detail-20261031-123356/report/index.html) |
| 04-dispute-detail | 2 | 14934 | 122.1 | x1.21 | 1384 | 3493 | 4119 | 0 | [index.html](r2/04-dispute-detail-20261101-010143/report/index.html) |
| 04-dispute-detail | 3 | 18755 | 153.8 | x1.53 | 1076 | 3717 | 4815 | 0 | [index.html](r3/04-dispute-detail-20261101-011204/report/index.html) |
