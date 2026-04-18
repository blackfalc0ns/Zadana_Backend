[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$CustomerIdentifier,

    [Parameter(Mandatory = $true)]
    [string]$CustomerPassword,

    [string]$VendorIdentifier,
    [string]$VendorPassword,
    [Guid]$VendorId,
    [Guid]$ProductId,
    [string]$SearchQuery,
    [Guid]$AddressId,
    [ValidateSet('cash', 'bank', 'card')]
    [string]$PaymentMethod = 'cash',
    [int]$Quantity = 1,
    [string]$Notes = 'debug order from script',
    [int]$WaitSeconds = 3,
    [switch]$SkipClearCart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Quantity -lt 1) {
    throw 'Quantity must be greater than zero.'
}

if (($VendorIdentifier -and -not $VendorPassword) -or (-not $VendorIdentifier -and $VendorPassword)) {
    throw 'VendorIdentifier and VendorPassword must either both be provided or both be omitted.'
}

if (-not $PSBoundParameters.ContainsKey('ProductId') -and [string]::IsNullOrWhiteSpace($SearchQuery)) {
    throw 'Provide either -ProductId or -SearchQuery.'
}

function Write-Step {
    param([string]$Message)
    Write-Host ("[{0}] {1}" -f (Get-Date).ToString('HH:mm:ss'), $Message)
}

function Test-HasGuidValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return $false
    }

    try {
        $guidValue = [Guid]$Value
        return $guidValue -ne [Guid]::Empty
    }
    catch {
        return $false
    }
}

function Resolve-ApiUri {
    param([string]$Path)

    $trimmedBase = $BaseUrl.TrimEnd('/')
    if ($Path.StartsWith('/')) {
        return "$trimmedBase$Path"
    }

    return "$trimmedBase/$Path"
}

function Get-ApiErrorMessage {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if ($ErrorRecord.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($ErrorRecord.ErrorDetails.Message)) {
        return $ErrorRecord.ErrorDetails.Message
    }

    $response = $ErrorRecord.Exception.Response
    if ($null -ne $response) {
        try {
            $stream = $response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
                if (-not [string]::IsNullOrWhiteSpace($body)) {
                    return $body
                }
            }
        }
        catch {
        }
    }

    return $ErrorRecord.Exception.Message
}

function Invoke-ZadanaApi {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('GET', 'POST', 'PATCH', 'PUT', 'DELETE')]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body,
        [string]$Token,
        [hashtable]$Headers
    )

    $requestHeaders = @{
        Accept = 'application/json'
    }

    if ($Token) {
        $requestHeaders['Authorization'] = "Bearer $Token"
    }

    if ($Headers) {
        foreach ($key in $Headers.Keys) {
            $requestHeaders[$key] = $Headers[$key]
        }
    }

    $invokeParams = @{
        Method  = $Method
        Uri     = (Resolve-ApiUri -Path $Path)
        Headers = $requestHeaders
    }

    if ($PSBoundParameters.ContainsKey('Body') -and $null -ne $Body) {
        $invokeParams['ContentType'] = 'application/json'
        $invokeParams['Body'] = ($Body | ConvertTo-Json -Depth 12 -Compress)
    }

    try {
        return Invoke-RestMethod @invokeParams
    }
    catch {
        $message = Get-ApiErrorMessage -ErrorRecord $_
        throw "API call failed: $Method $Path`n$message"
    }
}

function Get-TokenFromAuthResponse {
    param([object]$Response)

    $token = $Response.tokens.accessToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'Authentication response did not include tokens.accessToken.'
    }

    return $token
}

function Login-Customer {
    Write-Step "Logging in customer $CustomerIdentifier"
    $response = Invoke-ZadanaApi -Method POST -Path '/api/customers/auth/login' -Body @{
        identifier = $CustomerIdentifier
        password   = $CustomerPassword
    }

    return Get-TokenFromAuthResponse -Response $response
}

function Login-Vendor {
    if (-not $VendorIdentifier) {
        return $null
    }

    Write-Step "Logging in vendor $VendorIdentifier"
    $response = Invoke-ZadanaApi -Method POST -Path '/api/vendors/auth/login' -Body @{
        identifier = $VendorIdentifier
        password   = $VendorPassword
    }

    return Get-TokenFromAuthResponse -Response $response
}

function Resolve-ProductId {
    if (Test-HasGuidValue $ProductId) {
        Write-Step "Using explicit product id $ProductId"
        return [Guid]$ProductId
    }

    Write-Step "Searching product by query '$SearchQuery'"
    $encodedQuery = [Uri]::EscapeDataString($SearchQuery.Trim())
    $response = Invoke-ZadanaApi -Method GET -Path "/api/products/search?query=$encodedQuery&per_page=1"

    if (-not $response.items -or $response.items.Count -lt 1) {
        throw "No products found for query '$SearchQuery'."
    }

    $resolvedId = [Guid]$response.items[0].id
    Write-Step "Resolved product id $resolvedId from search"
    return $resolvedId
}

function Resolve-AddressId {
    param([string]$CustomerToken)

    if (Test-HasGuidValue $AddressId) {
        Write-Step "Using explicit address id $AddressId"
        return [Guid]$AddressId
    }

    Write-Step 'Loading customer addresses'
    $addresses = Invoke-ZadanaApi -Method GET -Path '/api/customers/addresses' -Token $CustomerToken
    if (-not $addresses -or $addresses.Count -lt 1) {
        throw 'Customer does not have any saved addresses.'
    }

    $selected = $addresses | Select-Object -First 1
    $defaultAddress = $addresses | Where-Object { $_.isDefault -eq $true } | Select-Object -First 1
    if ($defaultAddress) {
        $selected = $defaultAddress
    }

    $resolvedId = [Guid]$selected.id
    Write-Step "Resolved address id $resolvedId"
    return $resolvedId
}

function Resolve-VendorId {
    param(
        [string]$CustomerToken,
        [Guid]$ResolvedProductId
    )

    if (Test-HasGuidValue $VendorId) {
        Write-Step "Using explicit vendor id $VendorId"
        return [Guid]$VendorId
    }

    Write-Step 'Loading cart vendors'
    $cartVendors = Invoke-ZadanaApi -Method GET -Path '/api/cart/vendors' -Token $CustomerToken
    $vendors = @($cartVendors.vendors)

    if ($vendors.Count -eq 0) {
        throw "No vendors are available for product $ResolvedProductId in the current cart."
    }

    if ($vendors.Count -gt 1) {
        $vendorNames = ($vendors | ForEach-Object { "$($_.id) [$($_.productsCount)] $($_.name)" }) -join '; '
        throw "Multiple vendors can fulfill the cart. Re-run with -VendorId. Candidates: $vendorNames"
    }

    $resolvedId = [Guid]$vendors[0].id
    Write-Step "Resolved vendor id $resolvedId from cart vendors"
    return $resolvedId
}

function Get-CheckoutSummary {
    param(
        [string]$CustomerToken,
        [Guid]$ResolvedVendorId,
        [Guid]$ResolvedAddressId
    )

    $path = "/api/checkout/summary?vendor_id=$ResolvedVendorId&address_id=$ResolvedAddressId"
    return Invoke-ZadanaApi -Method GET -Path $path -Token $CustomerToken
}

function Get-VendorNotificationSnapshot {
    param([string]$VendorToken)

    if (-not $VendorToken) {
        return $null
    }

    return Invoke-ZadanaApi -Method GET -Path '/api/vendor/notifications?page=1&per_page=50&type=vendor_new_order' -Token $VendorToken
}

function Get-VendorOrdersSnapshot {
    param([string]$VendorToken)

    if (-not $VendorToken) {
        return $null
    }

    return Invoke-ZadanaApi -Method GET -Path '/api/vendor/orders?page=1&pageSize=20' -Token $VendorToken
}

$startedAtUtc = (Get-Date).ToUniversalTime()
$customerToken = Login-Customer
$vendorToken = Login-Vendor
$resolvedProductId = Resolve-ProductId
$resolvedAddressId = Resolve-AddressId -CustomerToken $customerToken

if (-not $SkipClearCart) {
    Write-Step 'Clearing customer cart'
    [void](Invoke-ZadanaApi -Method DELETE -Path '/api/cart' -Token $customerToken)
}

Write-Step "Adding product $resolvedProductId to cart"
$cartItemResponse = Invoke-ZadanaApi -Method POST -Path '/api/cart/items' -Token $customerToken -Body @{
    productId = $resolvedProductId
    quantity  = $Quantity
}

$beforeNotifications = Get-VendorNotificationSnapshot -VendorToken $vendorToken
$beforeOrders = Get-VendorOrdersSnapshot -VendorToken $vendorToken

$resolvedVendorId = Resolve-VendorId -CustomerToken $customerToken -ResolvedProductId $resolvedProductId
$checkoutSummary = Get-CheckoutSummary -CustomerToken $customerToken -ResolvedVendorId $resolvedVendorId -ResolvedAddressId $resolvedAddressId

$selectedDeliverySlot = $null
$availableDeliverySlot = @($checkoutSummary.delivery_slots | Where-Object { $_.is_available -eq $true } | Select-Object -First 1)
if ($availableDeliverySlot.Count -gt 0) {
    $selectedDeliverySlot = $availableDeliverySlot[0].id
}

Write-Step "Placing order using payment method '$PaymentMethod'"
$orderResponse = Invoke-ZadanaApi -Method POST -Path '/api/orders' -Token $customerToken -Body @{
    vendor_id        = $resolvedVendorId
    address_id       = $resolvedAddressId
    delivery_slot_id = $selectedDeliverySlot
    payment_method   = $PaymentMethod
    promo_code       = $null
    notes            = $Notes
}

$orderId = [string]$orderResponse.order.id

if ($WaitSeconds -gt 0) {
    Write-Step "Waiting $WaitSeconds second(s) before vendor-side checks"
    Start-Sleep -Seconds $WaitSeconds
}

$afterNotifications = Get-VendorNotificationSnapshot -VendorToken $vendorToken
$afterOrders = Get-VendorOrdersSnapshot -VendorToken $vendorToken

$matchedNotification = $null
$newVendorNotificationIds = @()
$orderVisibleInVendorOrders = $false
$matchedVendorOrder = $null
$paymentResponse = $null

if ($null -ne $orderResponse.PSObject.Properties['payment']) {
    $paymentResponse = $orderResponse.payment
}

if ($afterNotifications) {
    $beforeIds = @()
    if ($beforeNotifications -and $beforeNotifications.items) {
        $beforeIds = @($beforeNotifications.items | ForEach-Object { [string]$_.id })
    }

    $afterItems = @($afterNotifications.items)
    $newVendorNotificationIds = @(
        $afterItems |
        Where-Object { $beforeIds -notcontains ([string]$_.id) } |
        ForEach-Object { [string]$_.id }
    )

    $matchedNotification = $afterItems | Where-Object {
        $_.referenceId -and ([string]$_.referenceId).Equals($orderId, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1
}

if ($afterOrders) {
    $afterOrderItems = @($afterOrders.items)
    $matchedVendorOrder = $afterOrderItems | Where-Object {
        $_.id -and ([string]$_.id).Equals($orderId, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1

    $orderVisibleInVendorOrders = $null -ne $matchedVendorOrder
}

$result = [pscustomobject]@{
    requestedAtUtc = $startedAtUtc.ToString('o')
    inputs = [pscustomobject]@{
        baseUrl            = $BaseUrl
        paymentMethod      = $PaymentMethod
        quantity           = $Quantity
        resolvedProductId  = $resolvedProductId
        resolvedVendorId   = $resolvedVendorId
        resolvedAddressId  = $resolvedAddressId
        vendorChecksRan    = [bool]$vendorToken
        waitSeconds        = $WaitSeconds
    }
    cart = [pscustomobject]@{
        addItemMessage = $cartItemResponse.message
        summary        = $cartItemResponse.summary
    }
    checkout = $checkoutSummary
    order = [pscustomobject]@{
        id            = $orderResponse.order.id
        status        = $orderResponse.order.status
        paymentMethod = $orderResponse.order.payment_method
        paymentStatus = $orderResponse.order.payment_status
        totalPrice    = $orderResponse.order.total_price
        payment       = $paymentResponse
    }
    vendorDiagnostics = if ($vendorToken) {
        [pscustomobject]@{
            beforeNotificationCount = if ($beforeNotifications) { $beforeNotifications.total } else { $null }
            afterNotificationCount  = if ($afterNotifications) { $afterNotifications.total } else { $null }
            newNotificationIds      = $newVendorNotificationIds
            matchedNotification     = $matchedNotification
            orderVisibleInOrdersApi = $orderVisibleInVendorOrders
            matchedVendorOrder      = $matchedVendorOrder
        }
    }
    else {
        $null
    }
}

Write-Host ''
Write-Host '=== Debug Summary ==='
Write-Host ("Order ID: {0}" -f $orderResponse.order.id)
Write-Host ("Order status: {0}" -f $orderResponse.order.status)
Write-Host ("Payment status: {0}" -f $orderResponse.order.payment_status)
Write-Host ("Resolved vendor: {0}" -f $resolvedVendorId)

if ($vendorToken) {
    if ($matchedNotification) {
        Write-Host 'Vendor notification check: FOUND vendor_new_order linked to this order.'
    }
    elseif ($PaymentMethod -eq 'card') {
        Write-Host 'Vendor notification check: no linked notification yet. Card orders need payment confirmation before vendor notification is expected.'
    }
    else {
        Write-Host 'Vendor notification check: NOT FOUND for this order.'
    }

    if ($orderVisibleInVendorOrders) {
        Write-Host 'Vendor orders API check: order is visible.'
    }
    else {
        Write-Host 'Vendor orders API check: order is NOT visible.'
    }
}

Write-Host ''
$result | ConvertTo-Json -Depth 12
