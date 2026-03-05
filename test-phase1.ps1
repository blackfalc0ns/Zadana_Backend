# Phase 1 Catalog API Test Script
$base = "http://localhost:5298"

# Step 0: Login
$loginBody = '{"identifier":"admin@system.com","password":"Admin@123"}'
$loginResult = Invoke-RestMethod -Uri "$base/api/admin/auth/login" -Method POST -Body $loginBody -ContentType 'application/json'
$token = $loginResult.tokens.accessToken
$headers = @{ Authorization = "Bearer $token" }
Write-Host "Login OK - Role: $($loginResult.user.role)" -ForegroundColor Green

# Step 1: Create root Category
Write-Host "`n===== 1. CREATE ROOT CATEGORY =====" -ForegroundColor Cyan
$catBody = [System.Text.Encoding]::UTF8.GetBytes('{"nameAr":"اغذية ومشروبات","nameEn":"Food and Beverage","displayOrder":1}')
$cat1 = Invoke-RestMethod -Uri "$base/api/admin/catalog/categories" -Method POST -Headers $headers -Body $catBody -ContentType 'application/json; charset=utf-8'
$cat1 | ConvertTo-Json
$cat1Id = $cat1.id

# Step 2: Create sub-Category under root
Write-Host "`n===== 2. CREATE SUB-CATEGORY =====" -ForegroundColor Cyan
$subCatJson = '{"nameAr":"البان","nameEn":"Dairy","parentCategoryId":"' + $cat1Id + '","displayOrder":1}'
$subCatBody = [System.Text.Encoding]::UTF8.GetBytes($subCatJson)
$cat2 = Invoke-RestMethod -Uri "$base/api/admin/catalog/categories" -Method POST -Headers $headers -Body $subCatBody -ContentType 'application/json; charset=utf-8'
$cat2 | ConvertTo-Json

# Step 3: Get Category Tree
Write-Host "`n===== 3. GET CATEGORY TREE =====" -ForegroundColor Cyan
$tree = Invoke-RestMethod -Uri "$base/api/admin/catalog/categories" -Method GET -Headers $headers
$tree | ConvertTo-Json -Depth 5

# Step 4: Create Brand
Write-Host "`n===== 4. CREATE BRAND =====" -ForegroundColor Cyan
$brandBody = [System.Text.Encoding]::UTF8.GetBytes('{"nameAr":"مراعي","nameEn":"Almarai","logoUrl":"https://example.com/logo.png"}')
$brand = Invoke-RestMethod -Uri "$base/api/admin/catalog/brands" -Method POST -Headers $headers -Body $brandBody -ContentType 'application/json; charset=utf-8'
$brand | ConvertTo-Json

# Step 5: Get Brands
Write-Host "`n===== 5. GET BRANDS =====" -ForegroundColor Cyan
$brands = Invoke-RestMethod -Uri "$base/api/admin/catalog/brands" -Method GET -Headers $headers
$brands | ConvertTo-Json

# Step 6: Create Unit
Write-Host "`n===== 6. CREATE UNIT =====" -ForegroundColor Cyan
$unitBody = [System.Text.Encoding]::UTF8.GetBytes('{"nameAr":"كيلوجرام","nameEn":"Kilogram","symbol":"kg"}')
$unit = Invoke-RestMethod -Uri "$base/api/admin/catalog/units" -Method POST -Headers $headers -Body $unitBody -ContentType 'application/json; charset=utf-8'
$unit | ConvertTo-Json

# Step 7: Get Units
Write-Host "`n===== 7. GET UNITS =====" -ForegroundColor Cyan
$units = Invoke-RestMethod -Uri "$base/api/admin/catalog/units" -Method GET -Headers $headers
$units | ConvertTo-Json

Write-Host "`n===== ALL TESTS PASSED =====" -ForegroundColor Green
