<#
.SYNOPSIS
  W8-03: Load test cụm với 1 / 2 / 3 replica API sau Nginx, rồi lập bảng so sánh.

.DESCRIPTION
  Với mỗi số replica N:
    1. Bật db + redis + api1..apiN (docker-compose.yml + docker-compose.loadtest.yml), tắt các replica còn lại
    2. Sinh nginx/nginx.loadtest.generated.conf với N upstream rồi khởi động lại Nginx
    3. Đợi các replica sẵn sàng, chạy từng plan JMeter (profile "cluster")
  Cuối cùng gọi compare-cluster.ps1 để tạo summary.md + summary.csv.
  Kết quả: tests/jmeter/results/cluster/<thời gian>/r<N>/<plan>-<thời gian>/report/index.html

.EXAMPLE
  .\scripts\run-cluster.ps1                                  # 1,2,3 replica x 3 plan (~30 phút)
  .\scripts\run-cluster.ps1 -Replicas 1,2 -Plans 04-dispute-detail
  .\scripts\run-cluster.ps1 -NoBuild                         # bỏ qua build image nếu đã build
  .\scripts\run-cluster.ps1 -Extra "-Jthreads=300"
  .\scripts\run-cluster.ps1 -Replicas 2,3 -NoBuild -RunStamp 20261031-122739   # chạy tiếp vào lần trước
#>
param(
    [int[]]$Replicas = @(1, 2, 3),
    [string[]]$Plans = @("02-dashboard-stats", "03-users-orders-paging", "04-dispute-detail"),
    [string]$Profile = "cluster",
    [string]$Extra = "",
    [int]$PauseSeconds = 15,          # nghỉ giữa các plan cho hệ thống ổn định lại
    [switch]$NoBuild,
    [switch]$KeepRunning,              # không tắt cụm sau khi chạy xong
    [string]$RunStamp = ""             # chạy tiếp vào thư mục results\cluster\<RunStamp> đã có
)

$ErrorActionPreference = "Stop"
$jmRoot = Split-Path -Parent $PSScriptRoot                     # tests/jmeter
$repo   = (Resolve-Path (Join-Path $jmRoot "..\..")).Path      # thư mục gốc project
$env:COMPOSE_PROFILES = "r3"                                   # để compose biết service api3
$composeArgs = @("compose", "-f", "$repo\docker-compose.yml", "-f", "$repo\docker-compose.loadtest.yml")

function Dc {
    Write-Host ">> docker compose $($args -join ' ')" -ForegroundColor DarkGray
    & docker @composeArgs @args
    if ($LASTEXITCODE -ne 0) { throw "docker compose $($args -join ' ') lỗi (exit $LASTEXITCODE)" }
}

function Get-StatusCode([string]$url) {
    try { return (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).StatusCode }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

function Wait-Replicas([string[]]$services, [string]$since) {
    $deadline = (Get-Date).AddSeconds(180)
    foreach ($svc in $services) {
        $container = "ebay_api_" + $svc.Substring(3)
        while ($true) {
            $log = (cmd /c "docker logs --since $since $container 2>&1") -join "`n"   # qua cmd để stderr không thành lỗi PowerShell
            if ($log -match "Application started|Now listening on") { Write-Host "   $container sẵn sàng" -ForegroundColor Green; break }
            if ((Get-Date) -gt $deadline) { Write-Warning "$container chưa thấy log 'Application started' sau 180s — vẫn chạy tiếp"; break }
            Start-Sleep -Seconds 3
        }
    }
    # Kiểm tra qua Nginx: chưa đăng nhập -> 401 nghĩa là API đã trả lời
    for ($i = 0; $i -lt 30; $i++) {
        $code = Get-StatusCode "http://localhost:5000/api/Dashboard/metrics"
        if ($code -in 200, 401, 403) { return }
        Start-Sleep -Seconds 2
    }
    throw "API qua Nginx (http://localhost:5000) không phản hồi"
}

function Disable-Superadmin2FA {
    $line = Get-Content "$repo\.env" | Where-Object { $_ -match '^SA_PASSWORD=' }
    $pw = ($line -replace '^SA_PASSWORD=', '').Trim().Trim('"')
    & docker exec ebay_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$pw" -C -I -d CloneEbayDB `
        -Q "SET NOCOUNT ON; UPDATE [User] SET TwoFactorEnabled = 0 WHERE Email = 'superadmin@ebay.local'" | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning "Không tắt được 2FA của superadmin — kiểm tra lại nếu JMeter báo 401" }
}

# ── Chuẩn bị ─────────────────────────────────────────────────
cmd /c "docker info >nul 2>&1"
if ($LASTEXITCODE -ne 0) { throw "Docker chưa chạy. Mở Docker Desktop rồi chạy lại." }

$stamp   = if ($RunStamp) { $RunStamp } else { Get-Date -Format "yyyyMMdd-HHmmss" }
$runRoot = "results\cluster\$stamp"                             # tương đối so với tests/jmeter
New-Item -ItemType Directory -Force -Path (Join-Path $jmRoot $runRoot) | Out-Null
$template = Get-Content "$repo\nginx\nginx.loadtest.conf.template" -Raw
$allApis  = @("api1", "api2", "api3")

if (-not $NoBuild) { Dc build api1 }                            # api2/api3 dùng chung image ebayclone-api:loadtest
Dc up -d db redis

$first = $true
foreach ($n in $Replicas) {
    if ($n -lt 1 -or $n -gt 3) { throw "Replicas chỉ nhận 1..3" }
    $on  = @($allApis[0..($n - 1)])
    $off = @($allApis | Where-Object { $on -notcontains $_ })   # @() để 1 phần tử vẫn là mảng (splat không tách ký tự)
    Write-Host "`n========== $n replica: $($on -join ', ') ==========" -ForegroundColor Cyan

    if ($off.Count -gt 0) { Dc stop @off }
    $since = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    Dc up -d --no-build @on

    # Sinh config Nginx với N upstream
    $ups = ($on | ForEach-Object { "        server ${_}:8080 max_fails=3 fail_timeout=10s;" }) -join "`n"
    $conf = $template.Replace("__UPSTREAMS__", $ups) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText("$repo\nginx\nginx.loadtest.generated.conf", $conf, (New-Object System.Text.UTF8Encoding $false))
    Dc up -d --no-deps --force-recreate nginx

    Wait-Replicas $on $since
    if ($first) { Disable-Superadmin2FA; $first = $false }

    # Ghi lại cấu hình của lần chạy (để đưa vào báo cáo)
    & docker ps --filter "name=ebay_" --format "{{.Names}}`t{{.Status}}" |
        Out-File -Encoding utf8 (Join-Path $jmRoot "$runRoot\r$n-containers.txt")

    foreach ($p in $Plans) {
        & "$PSScriptRoot\run.ps1" -Plan $p -Profile $Profile -OutDir "$runRoot\r$n" -Extra $Extra
        & docker stats --no-stream --format "{{.Name}}`t{{.CPUPerc}}`t{{.MemUsage}}" |
            Out-File -Append -Encoding utf8 (Join-Path $jmRoot "$runRoot\r$n-docker-stats.txt")
        Start-Sleep -Seconds $PauseSeconds
    }
}

& "$PSScriptRoot\compare-cluster.ps1" -RunDir (Join-Path $jmRoot $runRoot)

if (-not $KeepRunning) {
    Write-Host "`n>> Tắt cụm load test" -ForegroundColor Yellow
    Dc stop
}
Write-Host ">> Xong. Bảng so sánh: tests\jmeter\$runRoot\summary.md" -ForegroundColor Green
