# 17. CI/CD: Jenkins, GitHub Actions

> Nhóm chọn **GitHub Actions**, không dùng Jenkins. Lý do: repo nằm sẵn trên GitHub, không phải dựng và bảo trì một server Jenkins, và runner `ubuntu-latest` có sẵn Docker để chạy test với SQL Server thật (Testcontainers).

## Mô tả trong dự án

| Workflow | File | Kích hoạt | Làm gì |
|---|---|---|---|
| **CI** | `.github/workflows/ci.yml` | Mỗi pull request vào `main` và mỗi push lên `main` | `dotnet restore` → `build` (Release) → **Application.UnitTests** → **Domain.UnitTests** → **Application.FunctionalTests** (SQL Server trong Docker qua Testcontainers, test HTTP thật) |
| **CD** | `.github/workflows/cd.yml` | Push lên `main` (tức là sau khi merge PR) | Build Docker image (`Dockerfile` nhiều giai đoạn: build React, build .NET, image runtime) → đẩy lên **GHCR** `ghcr.io/monomee/ebay_clone_adminrole`, gắn 2 tag: `latest` và `sha-<commit ngắn>`. Có cache layer (`type=gha`) |
| Azure (tùy chọn) | `.github/workflows/azure-dev.yml` | Chạy tay (`workflow_dispatch`) / push | Mẫu deploy lên Azure (azd) |

Quy trình làm việc:

```
nhánh feature (security/w9-...) → PR → CI chạy (đỏ thì không merge) → review → merge → CD build image sha-xxxx → k8s set image (file 20)
```

- Tag `sha-<commit>` làm cho mỗi bản deploy truy ngược được về đúng commit, và rollback được (`kubectl rollout undo`, hoặc set image về sha cũ).
- **Secret không nằm trong repo.** CD dùng `GITHUB_TOKEN` để đẩy lên GHCR. `k8s/secret.yaml`, `.env`, `appsettings.json` đều nằm trong `.gitignore`; trong repo chỉ có file `*.example`.
- Test trong CI **không gửi mail thật** (`IEmailService` bị thay bằng bản giả) và **không cần captcha thật**.

## Để làm gì

- **CI:** mỗi thay đổi được build và chạy toàn bộ test **trước khi** vào `main`. Lỗi bị bắt ở PR, không phải trên production.
- **CD:** merge xong là có image mới, không ai phải build tay trên máy cá nhân (thiếu file, khác phiên bản).
- Kết hợp với k8s (file 20): từ commit tới bản đang chạy là một luồng tự động và lặp lại được.

## Kịch bản demo

### Demo 1: lịch sử chạy (1 phút)

1. Mở `https://github.com/Monomee/ebay_clone_adminRole/actions`.
2. Chỉ vào các lần chạy gần nhất:
   - **CI** trên PR `feat(security): sign-up IP blocklist...` (PR #31): ✅ khoảng 2 phút 50 giây;
   - **CI** và **CD** chạy khi merge PR #30 vào `main`: ✅.
3. Mở một lần chạy CI, xem log các bước `Run unit tests`, `Run functional tests`: số test pass, có dòng Testcontainers khởi động SQL Server.

Hoặc trong terminal:

```bash
gh run list -L 5
gh run view <run-id> --log | grep -E "Passed!|Failed!"
```

### Demo 2: PR có test hỏng sẽ đỏ (2 phút, làm trước buổi demo rồi chụp màn hình)

```bash
git checkout -b demo/ci-red
# sửa 1 assert trong tests/Application.UnitTests/Security/IpBlocklistTests.cs cho sai, ví dụ Is.EqualTo(1) → Is.EqualTo(2)
git commit -am "demo: CI phát hiện test hỏng" && git push -u origin demo/ci-red
gh pr create --title "demo: CI đỏ" --body "Demo CI" --draft
```

- Tab **Checks** của PR hiện ❌ `Build & Test`, chỉ rõ test nào fail.
- Nút **Merge** bị khóa (*Required status check "Build & Test" is failing*), kể cả với admin.
- Thử `git push origin HEAD:main` thẳng lên `main`: bị từ chối (*protected branch*).
- Sau demo: `gh pr close --delete-branch`.

### Demo 3: image trên GHCR (1 phút)

1. Mở `https://github.com/Monomee/ebay_clone_adminRole/pkgs/container/ebay_clone_adminrole` (hoặc tab **Packages** của repo).
2. Thấy các tag `latest` và `sha-xxxxxxx`, mỗi tag ứng với một lần merge.
3. Chạy:
   ```bash
   docker pull ghcr.io/monomee/ebay_clone_adminrole:sha-<tag>
   ```
   Image này chính là đầu vào cho rolling update ở file 20.

## Câu hỏi hay gặp

| Câu hỏi | Trả lời |
|---|---|
| Sao CD chưa tự `kubectl apply`? | Cluster thật chưa có. CD dừng ở bước đẩy image. Bước deploy chạy bằng `scripts/demo-rolling-update.sh` (file 20). Khi có cluster thì thêm job `kubectl set image` với kubeconfig để trong GitHub Secrets |
| Có chặn merge khi CI đỏ không? | Có. Branch protection của `main` (bật 08/11): bắt buộc qua PR, check `Build & Test` phải xanh, áp dụng cả cho admin, cấm force push và xóa nhánh. PR có CI đỏ thì nút Merge bị khóa |
