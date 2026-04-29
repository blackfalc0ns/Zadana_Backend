$ErrorActionPreference = "Continue"
$BASE = "http://localhost:5298"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " DELIVERY OFFER RUNTIME TEST" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# ─── Step 1: Login as Driver ─────────────────
Write-Host "[1/5] Logging in as driver (driver.active@zadana.local)..." -ForegroundColor Yellow
try {
    $driverLogin = Invoke-RestMethod -Uri "$BASE/api/drivers/auth/login" -Method POST -ContentType "application/json" -Body '{"identifier":"driver.active@zadana.local","password":"Zadana@12345"}' -TimeoutSec 30
    $driverToken = $driverLogin.token
    $driverUserId = $driverLogin.userId
    Write-Host "  Driver UserId: $driverUserId" -ForegroundColor Green
    Write-Host "  Token: $($driverToken.Substring(0,40))..." -ForegroundColor Green
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    # Try phone login
    Write-Host "  Trying phone login (01000000020)..." -ForegroundColor Yellow
    try {
        $driverLogin = Invoke-RestMethod -Uri "$BASE/api/drivers/auth/login" -Method POST -ContentType "application/json" -Body '{"identifier":"01000000020","password":"Zadana@12345"}' -TimeoutSec 30
        $driverToken = $driverLogin.token
        $driverUserId = $driverLogin.userId
        Write-Host "  Driver UserId: $driverUserId" -ForegroundColor Green
        Write-Host "  Token: $($driverToken.Substring(0,40))..." -ForegroundColor Green
    } catch {
        Write-Host "  PHONE LOGIN ALSO FAILED: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# ─── Step 2: Login as Vendor ─────────────────
Write-Host "`n[2/5] Logging in as vendor (vendor.test1@zadana.local)..." -ForegroundColor Yellow
try {
    $vendorLogin = Invoke-RestMethod -Uri "$BASE/api/vendors/auth/login" -Method POST -ContentType "application/json" -Body '{"identifier":"vendor.test1@zadana.local","password":"Zadana@12345"}' -TimeoutSec 30
    $vendorToken = $vendorLogin.token
    $vendorUserId = $vendorLogin.userId
    Write-Host "  Vendor UserId: $vendorUserId" -ForegroundColor Green
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# ─── Step 3: Get driver home (before offer) ──
Write-Host "`n[3/5] Checking driver home state (before dispatch)..." -ForegroundColor Yellow
$driverHeaders = @{ Authorization = "Bearer $driverToken" }
try {
    $homeBefore = Invoke-RestMethod -Uri "$BASE/api/drivers/home" -Method GET -Headers $driverHeaders -TimeoutSec 60
    Write-Host "  homeState: $($homeBefore.homeState)" -ForegroundColor Green
    Write-Host "  currentOffer: $(if ($homeBefore.currentOffer) { 'EXISTS (assignmentId=' + $homeBefore.currentOffer.assignmentId + ')' } else { 'null' })" -ForegroundColor $(if ($homeBefore.currentOffer) { "Green" } else { "White" })
    Write-Host "  isAvailable: $($homeBefore.operationalStatus.isAvailable)" -ForegroundColor White
    Write-Host "  isOperational: $($homeBefore.operationalStatus.isOperational)" -ForegroundColor White
    Write-Host "  gateStatus: $($homeBefore.operationalStatus.gateStatus)" -ForegroundColor White
    Write-Host "  canReceiveOffers: $($homeBefore.operationalStatus.canReceiveOffers)" -ForegroundColor White
    Write-Host "  verificationStatus: $($homeBefore.operationalStatus.verificationStatus)" -ForegroundColor White

    # If driver has an existing offer already, show it
    if ($homeBefore.currentOffer) {
        Write-Host "`n  *** EXISTING OFFER FOUND ***" -ForegroundColor Green
        Write-Host "    assignmentId: $($homeBefore.currentOffer.assignmentId)" -ForegroundColor Green
        Write-Host "    orderId: $($homeBefore.currentOffer.orderId)" -ForegroundColor Green
        Write-Host "    orderNumber: $($homeBefore.currentOffer.orderNumber)" -ForegroundColor Green
        Write-Host "    vendorName: $($homeBefore.currentOffer.vendorName)" -ForegroundColor Green
        Write-Host "    deliveryFee: $($homeBefore.currentOffer.deliveryFee)" -ForegroundColor Green
        Write-Host "    countdownSeconds: $($homeBefore.currentOffer.countdownSeconds)" -ForegroundColor Green
    }

    # If driver has current assignment
    if ($homeBefore.currentAssignment) {
        Write-Host "`n  *** ACTIVE ASSIGNMENT ***" -ForegroundColor Yellow
        Write-Host "    assignmentId: $($homeBefore.currentAssignment.assignmentId)" -ForegroundColor Yellow
        Write-Host "    orderId: $($homeBefore.currentAssignment.orderId)" -ForegroundColor Yellow
        Write-Host "    status: $($homeBefore.currentAssignment.status)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# ─── Step 4: Set driver as available if needed ──
if ($homeBefore -and -not $homeBefore.operationalStatus.isAvailable) {
    Write-Host "`n[4a] Setting driver as available..." -ForegroundColor Yellow
    try {
        Invoke-RestMethod -Uri "$BASE/api/drivers/me/availability" -Method PUT -Headers $driverHeaders -ContentType "application/json" -Body '{"isAvailable":true}' -TimeoutSec 30
        Write-Host "  Driver set to available" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ─── Step 5: Check vendor orders ──
Write-Host "`n[4/5] Looking for orders we can test with..." -ForegroundColor Yellow
$vendorHeaders = @{ Authorization = "Bearer $vendorToken" }

# Check different statuses
foreach ($status in @("Preparing", "DriverAssignmentInProgress", "ReadyForPickup", "Accepted")) {
    try {
        $ordersResp = Invoke-RestMethod -Uri "$BASE/api/vendor/orders?status=$status" -Method GET -Headers $vendorHeaders -TimeoutSec 30
        $count = if ($ordersResp.items) { $ordersResp.items.Count } elseif ($ordersResp.Count) { $ordersResp.Count } else { 0 }
        Write-Host "  Orders in '$status': $count" -ForegroundColor White
        
        if ($count -gt 0) {
            $items = if ($ordersResp.items) { $ordersResp.items } else { $ordersResp }
            foreach ($order in $items) {
                Write-Host "    - $($order.orderNumber) ($($order.id))" -ForegroundColor Gray
            }
        }
    } catch {
        Write-Host "  Failed to query '$status' orders: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ─── Step 6: Final driver home check ──
Write-Host "`n[5/5] Final driver home state check..." -ForegroundColor Yellow
try {
    $homeAfter = Invoke-RestMethod -Uri "$BASE/api/drivers/home" -Method GET -Headers $driverHeaders -TimeoutSec 60
    Write-Host "  homeState: $($homeAfter.homeState)" -ForegroundColor $(if ($homeAfter.homeState -eq "IncomingOffer") { "Green" } else { "Yellow" })
    Write-Host "  currentOffer: $(if ($homeAfter.currentOffer) { 'EXISTS' } else { 'null' })" -ForegroundColor $(if ($homeAfter.currentOffer) { "Green" } else { "Yellow" })
    
    if ($homeAfter.currentOffer) {
        Write-Host "    assignmentId: $($homeAfter.currentOffer.assignmentId)" -ForegroundColor Green
        Write-Host "    orderId: $($homeAfter.currentOffer.orderId)" -ForegroundColor Green
        Write-Host "    orderNumber: $($homeAfter.currentOffer.orderNumber)" -ForegroundColor Green
        Write-Host "    vendorName: $($homeAfter.currentOffer.vendorName)" -ForegroundColor Green
        Write-Host "    countdownSeconds: $($homeAfter.currentOffer.countdownSeconds)" -ForegroundColor Green
    }
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " TEST COMPLETE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
