<#
.SYNOPSIS
  Script kiểm thử tự động Rate Limiting 2 lớp (Nginx + ASP.NET Core App) và bắt mã lỗi HTTP 429.

.DESCRIPTION
  - Test 1: Kiểm thử Rate Limiting tại Lớp 2 (ASP.NET Core Application - StrictPolicy 5 req/min).
            Gửi 5 request login sai -> 200, request thứ 6 -> 429 với Retry-After.
  - Test 2: Kiểm thử Rate Limiting tại Lớp 1 (Nginx Gateway - limit_req burst protection).
            Bắn flood liên tiếp nhiều request để Nginx chặn ngay tại cửa ngõ -> 429.

.EXAMPLE
  .\scripts\test-rate-limit.ps1 -BaseUrl "http://localhost:5000"
#>

param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "     KIỂM THỬ RATE LIMITING 2 LỚP & HTTP 429" -ForegroundColor Cyan
Write-Host "     Target URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Cyan

# -------------------------------------------------------------------------
# TEST 1: KIỂM THỬ RATE LIMIT TẠI LỚP 2 (ASP.NET Core App - StrictPolicy)
# -------------------------------------------------------------------------
Write-Host ">>> [TEST 1] Kiểm thử Lớp 2: Application Layer (StrictPolicy: 5 req/phút)" -ForegroundColor Yellow
Write-Host "Kỳ vọng: 5 request đầu trả về HTTP 200, request thứ 6 trả về HTTP 429`n" -ForegroundColor DarkGray

$loginUrl = "$BaseUrl/api/Auth/login"
$body = @{
    userName = "superadmin@ebay.local"
    password = "WrongPassword123!"
} | ConvertTo-Json

for ($i = 1; $i -le 6; $i++) {
    try {
        $response = Invoke-WebRequest -Uri $loginUrl -Method POST -Body $body -ContentType "application/json" -SkipHttpErrorCheck
        $statusCode = [int]$response.StatusCode
        $rateLimitSource = $response.Headers["X-RateLimit-Source"]
        $retryAfter = $response.Headers["Retry-After"]
        $backendServer = $response.Headers["X-Backend-Server"]

        if ($statusCode -eq 200) {
            Write-Host "  Request $i: HTTP $statusCode OK (Login failed - đúng mật khẩu sai) | Backend: $backendServer" -ForegroundColor Green
        } elseif ($statusCode -eq 429) {
            Write-Host "  Request $i: HTTP 429 TOO MANY REQUESTS! | Source: $rateLimitSource | Retry-After: $retryAfter s" -ForegroundColor Magenta
            Write-Host "  --> Body: $($response.Content)" -ForegroundColor DarkMagenta
        } else {
            Write-Host "  Request $i: HTTP $statusCode | Backend: $backendServer" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Request $i: Lỗi kết nối - $_" -ForegroundColor Red
    }
}

# -------------------------------------------------------------------------
# TEST 2: KIỂM THỬ RATE LIMIT TẠI LỚP 1 (Nginx Gateway - Burst Overflow)
# -------------------------------------------------------------------------
Write-Host "`n>>> [TEST 2] Kiểm thử Lớp 1: Nginx Gateway Layer (Flood Burst Overflow)" -ForegroundColor Yellow
Write-Host "Kỳ vọng: Khi bắn flood request liên tục, Nginx sẽ chặn ngay tại gateway và trả về HTTP 429`n" -ForegroundColor DarkGray

$blockedByNginxCount = 0
$passedCount = 0

1..25 | ForEach-Object {
    $reqIndex = $_
    try {
        $resp = Invoke-WebRequest -Uri "$BaseUrl/api/Auth/login" -Method POST -Body $body -ContentType "application/json" -SkipHttpErrorCheck
        $code = [int]$resp.StatusCode
        $src = $resp.Headers["X-RateLimit-Source"]

        if ($code -eq 429 -and $src -like "*Nginx*") {
            $blockedByNginxCount++
            Write-Host "  Flood #$reqIndex: HTTP 429 (Chặn bởi NGINX GATEWAY)" -ForegroundColor Red
        } elseif ($code -eq 429) {
            Write-Host "  Flood #$reqIndex: HTTP 429 (Chặn bởi Application)" -ForegroundColor Magenta
        } else {
            $passedCount++
            Write-Host "  Flood #$reqIndex: HTTP $code" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  Flood #$reqIndex: Lỗi kết nối" -ForegroundColor DarkRed
    }
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "KẾT QUẢ TỔNG HỢP KIỂM THỬ RATE LIMITING:" -ForegroundColor Cyan
Write-Host "- Lớp 2 (App RateLimiter): Kích hoạt khi đạt 5 req/phút tại /api/Auth/login" -ForegroundColor White
Write-Host "- Lớp 1 (Nginx Gateway): Kích hoạt khi có lượng request dồn dập (burst > limit)" -ForegroundColor White
Write-Host "============================================================`n" -ForegroundColor Cyan
