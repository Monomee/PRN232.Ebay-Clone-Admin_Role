<#
.SYNOPSIS
  Đọc statistics.json của từng lần chạy trong results/cluster/<thời gian>/r<N>/
  và tạo bảng so sánh summary.md + summary.csv (throughput, p95, error rate).

.EXAMPLE
  .\scripts\compare-cluster.ps1 -RunDir results\cluster\20261031-210000
#>
param([Parameter(Mandatory = $true)][string]$RunDir)

$ErrorActionPreference = "Stop"
$RunDir = (Resolve-Path $RunDir).Path
$rows = @()

Get-ChildItem $RunDir -Directory | Where-Object { $_.Name -match '^r(\d+)$' } | Sort-Object { [int]($_.Name.Substring(1)) } | ForEach-Object {
    $n = [int]$_.Name.Substring(1)
    Get-ChildItem $_.FullName -Directory | ForEach-Object {
        $stats = Join-Path $_.FullName "report\statistics.json"
        if (-not (Test-Path $stats)) { Write-Warning "Thiếu $stats"; return }
        $t = (Get-Content $stats -Raw -Encoding UTF8 | ConvertFrom-Json).Total
        $plan = $_.Name -replace '-\d{8}-\d{6}$', ''
        $rows += [pscustomobject]@{
            Plan       = $plan
            Replicas   = $n
            Samples    = [int]$t.sampleCount
            Throughput = [math]::Round([double]$t.throughput, 1)
            MeanMs     = [math]::Round([double]$t.meanResTime, 0)
            P95Ms      = [math]::Round([double]$t.pct2ResTime, 0)
            P99Ms      = [math]::Round([double]$t.pct3ResTime, 0)
            ErrorPct   = [math]::Round([double]$t.errorPct, 2)
            Report     = ((Join-Path $_.FullName "report\index.html").Substring($RunDir.Length + 1)) -replace '\\', '/'
        }
    }
}
if (-not $rows) { throw "Không tìm thấy kết quả trong $RunDir" }

$rows | Export-Csv (Join-Path $RunDir "summary.csv") -NoTypeInformation -Encoding UTF8

$md = @("# W8-03 — So sánh load test theo số replica", "",
        "Thư mục: ``$(Split-Path $RunDir -Leaf)`` — mỗi replica giới hạn 1 CPU / 768 MB, Nginx round-robin, không rate limit.", "",
        "| Kịch bản | Replica | Samples | Throughput (req/s) | So với 1 replica | Mean (ms) | p95 (ms) | p99 (ms) | Error % | HTML report |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---|")
foreach ($g in ($rows | Group-Object Plan)) {
    $base = ($g.Group | Where-Object Replicas -eq 1 | Select-Object -First 1).Throughput
    foreach ($r in ($g.Group | Sort-Object Replicas)) {
        $ratio = if ($base) { "x{0:N2}" -f ($r.Throughput / $base) } else { "-" }
        $md += "| $($r.Plan) | $($r.Replicas) | $($r.Samples) | $($r.Throughput) | $ratio | $($r.MeanMs) | $($r.P95Ms) | $($r.P99Ms) | $($r.ErrorPct) | [index.html]($($r.Report)) |"
    }
}
$md -join "`r`n" | Out-File (Join-Path $RunDir "summary.md") -Encoding UTF8
$rows | Format-Table Plan, Replicas, Throughput, P95Ms, ErrorPct -AutoSize
Write-Host ">> $RunDir\summary.md" -ForegroundColor Green
