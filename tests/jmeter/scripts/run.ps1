<#
.SYNOPSIS
  Chạy 1 test plan JMeter ở chế độ non-GUI và xuất HTML report.

.EXAMPLE
  .\scripts\run.ps1 -Plan 02-dashboard-stats
  .\scripts\run.ps1 -Plan 02-dashboard-stats -Profile load-500
  .\scripts\run.ps1 -Plan 03-users-orders-paging -Extra "-Jthreads=200"
  .\scripts\run.ps1 -Plan 02-dashboard-stats -OutDir results\baseline
#>
param(
    [Parameter(Mandatory = $true)][string]$Plan,     # tên file trong plans/, không cần .jmx
    [string]$Profile = "",                            # vd: load-500 -> config/load-500.properties
    [string]$OutDir = "results",                      # thư mục gốc chứa kết quả
    [string]$Extra = ""                               # tham số -J bổ sung
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot             # tests/jmeter
Set-Location $root

# Tìm jmeter: ưu tiên biến JMETER_HOME, sau đó PATH
$jmeter = if ($env:JMETER_HOME) { Join-Path $env:JMETER_HOME "bin\jmeter.bat" } else { "jmeter.bat" }

$planFile = Join-Path "plans" "$Plan.jmx"
if (-not (Test-Path $planFile)) { throw "Không tìm thấy $planFile" }

$stamp  = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $OutDir "$Plan-$stamp"
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

$jmArgs = @(
    "-n",
    "-t", $planFile,
    "-q", "config\local.properties"
)
if ($Profile) { $jmArgs += @("-q", "config\$Profile.properties") }
$jmArgs += @(
    "-l", "$runDir\results.jtl",
    "-j", "$runDir\jmeter.log",
    "-e", "-o", "$runDir\report"
)
if ($Extra) { $jmArgs += $Extra.Split(" ") }

Write-Host ">> $jmeter $($jmArgs -join ' ')" -ForegroundColor Cyan
& $jmeter @jmArgs
Write-Host ">> Report: $runDir\report\index.html" -ForegroundColor Green
