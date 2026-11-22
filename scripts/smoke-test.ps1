param(
    [string]$Api = "http://localhost:5130",
    [string]$Web = "http://localhost:5173",
    [switch]$IncludeRateLimit
)

$ErrorActionPreference = "Stop"
$script:failures = 0

# Satu fungsi untuk semua permintaan. Mengembalikan status walau permintaannya ditolak (4xx/5xx).
function Invoke-Http {
    param([string]$Method, [string]$Uri, [hashtable]$Headers = @{}, [string]$Body = "")

    try {
        $params = @{ Method = $Method; Uri = $Uri; Headers = $Headers; UseBasicParsing = $true }
        if ($Body) { $params.Body = $Body; $params.ContentType = "application/json" }

        $response = Invoke-WebRequest @params
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Headers = $response.Headers; Content = $response.Content }
    }
    catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        return [pscustomobject]@{ Status = $status; Headers = @{}; Content = "$($_.ErrorDetails.Message)" }
    }
}

# Membaca satu header (berfungsi di Windows PowerShell 5 maupun PowerShell 7)
function Get-HeaderValue($Response, [string]$Name) {
    try { $value = $Response.Headers[$Name] } catch { return "" }
    if ($null -eq $value) { return "" }
    if ($value -is [array]) { return ($value -join ",") }
    return [string]$value
}

function Check([string]$Name, [bool]$Passed, [string]$Detail = "") {
    if ($Passed) {
        Write-Host "PASS  $Name" -ForegroundColor Green
    }
    else {
        Write-Host "FAIL  $Name $Detail" -ForegroundColor Red
        $script:failures++
    }
}

function Sign-In([string]$Email) {
    $body = (@{ email = $Email; password = "DemoPassword123!" } | ConvertTo-Json -Compress)
    return Invoke-Http -Method POST -Uri "$Api/api/v1/auth/login" -Headers @{ Origin = $Web } -Body $body
}

Write-Host "`n== API ==" -ForegroundColor Cyan

$health = Invoke-Http -Method GET -Uri "$Api/health"
Check "GET /health is Healthy (PostgreSQL and Redis)" ($health.Status -eq 200 -and $health.Content -match '"status"\s*:\s*"Healthy"') "(status $($health.Status))"
Check "Security headers on API responses (nosniff, no-store)" `
    ((Get-HeaderValue $health "X-Content-Type-Options") -eq "nosniff" -and (Get-HeaderValue $health "Cache-Control") -match "no-store")
Check "API does not announce its server software" ([string]::IsNullOrEmpty((Get-HeaderValue $health "Server")))

$anonymous = Invoke-Http -Method GET -Uri "$Api/api/v1/auth/me"
Check "A request without a token is rejected (401)" ($anonymous.Status -eq 401) "(status $($anonymous.Status))"

$preflight = Invoke-Http -Method OPTIONS -Uri "$Api/api/v1/incidents" -Headers @{
    Origin                           = $Web
    "Access-Control-Request-Method"  = "POST"
    "Access-Control-Request-Headers" = "authorization,content-type"
}
Check "CORS preflight is answered for the web origin" `
    ($preflight.Status -in 200, 204 -and $preflight.Headers) "(status $($preflight.Status))"

$admin = Sign-In "admin@opspilot.local"
$token = $null
if ($admin.Status -eq 200) { $token = ($admin.Content | ConvertFrom-Json).accessToken }
Check "Demo admin can sign in (needs SEED_DEMO_DATA=true)" ($null -ne $token) "(status $($admin.Status))"
Check "Sign-in response allows the web origin (CORS)" ((Get-HeaderValue $admin "Access-Control-Allow-Origin") -eq $Web)

if ($token) {
    $auth = @{ Authorization = "Bearer $token" }

    $me = Invoke-Http -Method GET -Uri "$Api/api/v1/auth/me" -Headers $auth
    Check "GET /auth/me returns the Admin" ($me.Status -eq 200 -and ($me.Content | ConvertFrom-Json).role -eq "Admin")

    $users = Invoke-Http -Method GET -Uri "$Api/api/v1/users" -Headers $auth
    Check "Admin can list users" ($users.Status -eq 200)

    $dashboard = Invoke-Http -Method GET -Uri "$Api/api/v1/dashboard/summary" -Headers $auth
    Check "Dashboard summary works (Redis cache path)" ($dashboard.Status -eq 200)
}

$viewer = Sign-In "viewer@opspilot.local"
if ($viewer.Status -eq 200) {
    $viewerAuth = @{ Authorization = "Bearer $(($viewer.Content | ConvertFrom-Json).accessToken)" }
    $forbidden = Invoke-Http -Method GET -Uri "$Api/api/v1/users" -Headers $viewerAuth
    Check "A Viewer cannot list users (403)" ($forbidden.Status -eq 403) "(status $($forbidden.Status))"
}
else {
    Check "Demo viewer can sign in" $false "(status $($viewer.Status))"
}

Write-Host "`n== Web ==" -ForegroundColor Cyan

$index = Invoke-Http -Method GET -Uri "$Web/"
Check "Web serves the application" ($index.Status -eq 200 -and $index.Content -match 'id="root"') "(status $($index.Status))"

$deepLink = Invoke-Http -Method GET -Uri "$Web/incidents/does-not-exist"
Check "A deep link does not 404 on reload (falls back to index.html)" ($deepLink.Status -eq 200 -and $deepLink.Content -match 'id="root"')

$missing = Invoke-Http -Method GET -Uri "$Web/assets/missing.js"
Check "A missing asset is a real 404, not index.html" ($missing.Status -eq 404) "(status $($missing.Status))"

Check "Content-Security-Policy is sent" ((Get-HeaderValue $index "Content-Security-Policy") -match "default-src 'self'")
Check "CSP allows the API origin" ((Get-HeaderValue $index "Content-Security-Policy") -match [regex]::Escape($Api))
Check "X-Content-Type-Options and X-Frame-Options are sent" `
    ((Get-HeaderValue $index "X-Content-Type-Options") -eq "nosniff" -and (Get-HeaderValue $index "X-Frame-Options") -eq "DENY")
Check "nginx does not announce its version" (-not ((Get-HeaderValue $index "Server") -match "\d"))

if ($IncludeRateLimit) {
    Write-Host "`n== Rate limiting ==" -ForegroundColor Cyan

    $limited = $false
    1..15 | ForEach-Object {
        # Email yang tidak ada: tidak mengunci akun demo sungguhan
        $attempt = Invoke-Http -Method POST -Uri "$Api/api/v1/auth/login" -Body '{"email":"nobody@opspilot.local","password":"wrong"}'
        if ($attempt.Status -eq 429) { $limited = $true }
    }

    Check "Repeated sign-in attempts are answered with 429" $limited
    Write-Host "Wait about a minute before signing in again from the browser." -ForegroundColor Yellow
}

Write-Host ""
if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "All checks passed." -ForegroundColor Green