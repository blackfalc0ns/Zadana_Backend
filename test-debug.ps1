# Debug - Capture full error response body
$base = "http://localhost:5298"

# Login
$body = '{"identifier":"admin@system.com","password":"Admin@123"}'
$r = Invoke-RestMethod -Uri "$base/api/admin/auth/login" -Method POST -Body $body -ContentType 'application/json'
$token = $r.tokens.accessToken
Write-Host "Login OK" -ForegroundColor Green

# Test POST Category with full error capture
Write-Host "`n===== POST CATEGORY (debug) =====" -ForegroundColor Cyan
try {
    $catBody = [System.Text.Encoding]::UTF8.GetBytes('{"nameAr":"Test","nameEn":"Test","displayOrder":1}')
    $result = Invoke-WebRequest -Uri "$base/api/admin/catalog/categories" -Method POST -Headers @{Authorization="Bearer $token"} -Body $catBody -ContentType 'application/json; charset=utf-8'
    Write-Host "SUCCESS: $($result.StatusCode)" -ForegroundColor Green
    Write-Host $result.Content
} catch {
    $resp = $_.Exception.Response
    Write-Host "STATUS: $($resp.StatusCode)" -ForegroundColor Red
    $stream = $resp.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $errBody = $reader.ReadToEnd()
    Write-Host "ERROR BODY:" -ForegroundColor Red
    Write-Host $errBody
}
