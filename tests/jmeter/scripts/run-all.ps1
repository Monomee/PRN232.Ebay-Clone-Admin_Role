<#
.SYNOPSIS
  Chạy toàn bộ kịch bản theo thứ tự. Kịch bản 01 (brute-force) chạy cuối
  và đợi 65s trước đó, vì rate limit login (5 req/60s) dùng chung toàn hệ thống.

.EXAMPLE
  .\scripts\run-all.ps1                         # chạy thử, lưu vào results/
  .\scripts\run-all.ps1 -OutDir results\baseline  # chạy baseline để commit
#>
param([string]$OutDir = "results", [string]$Profile = "")

$plans = @("02-dashboard-stats", "03-users-orders-paging", "04-dispute-detail")
foreach ($p in $plans) {
    & "$PSScriptRoot\run.ps1" -Plan $p -Profile $Profile -OutDir $OutDir
}

Write-Host ">> Đợi 65s để rate limit login reset..." -ForegroundColor Yellow
Start-Sleep -Seconds 65
& "$PSScriptRoot\run.ps1" -Plan "01-login-bruteforce" -OutDir $OutDir
