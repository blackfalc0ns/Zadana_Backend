<#
.SYNOPSIS
    Generates fresh random secrets for Zadana backend and prints them as
    environment variable export commands.

.DESCRIPTION
    Run this once to bootstrap a brand new environment, or whenever you need
    to rotate the symmetric secrets the app owns directly:
      - JwtSettings:Secret           (64-byte random, base64)
      - BankTransfer:WebhookSecret   (32-byte random, hex)
      - Seeding:ManagementKey        (32-byte random, hex)

    Provider-issued secrets (Moyasar, ImageKit, Resend, OneSignal, MS SQL
    password) must be rotated through each provider's dashboard. This script
    only emits placeholders for them so the operator pastes the new values.

.OUTPUTS
    Bash + PowerShell exports plus an env file you can copy into your
    deployment platform. NEVER commit the output.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('powershell', 'bash', 'env')]
    [string]$Format = 'env'
)

function New-RandomBase64([int]$ByteLength) {
    $bytes = New-Object byte[] $ByteLength
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes)
}

function New-RandomHex([int]$ByteLength) {
    $bytes = New-Object byte[] $ByteLength
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
}

$jwtSecret = New-RandomBase64 64
$bankWebhook = New-RandomHex 32
$seedingKey = New-RandomHex 32

$values = [ordered]@{
    'JwtSettings__Secret'                       = $jwtSecret
    'BankTransfer__WebhookSecret'               = $bankWebhook
    'Seeding__ManagementKey'                    = $seedingKey

    # Placeholders for provider-issued secrets — replace by hand.
    'ConnectionStrings__DefaultConnection'      = '<<rotate via DBaaS panel; format: Server=...;Encrypt=True;TrustServerCertificate=False;User Id=...;Password=...>>'
    'ResendSettings__ApiKey'                    = '<<rotate at https://resend.com/api-keys>>'
    'TwilioSettings__AccountSid'                = '<<rotate at https://console.twilio.com>>'
    'TwilioSettings__AuthToken'                 = '<<rotate at https://console.twilio.com>>'
    'TwilioSettings__FromNumber'                = '<<your provisioned Twilio number>>'
    'ImageKit__PublicKey'                       = '<<rotate at https://imagekit.io/dashboard/developer/api-keys>>'
    'ImageKit__PrivateKey'                      = '<<rotate at https://imagekit.io/dashboard/developer/api-keys>>'
    'ImageKit__UrlEndpoint'                     = '<<your ImageKit endpoint, e.g. https://ik.imagekit.io/your-id>>'
    'Moyasar__PublishableKey'                   = '<<rotate at https://dashboard.moyasar.com>>'
    'Moyasar__SecretKey'                        = '<<rotate at https://dashboard.moyasar.com>>'
    'Moyasar__WebhookSecret'                    = '<<rotate at https://dashboard.moyasar.com>>'
    'Moyasar__CallbackUrl'                      = 'https://your-domain/api/payments/moyasar/verify'
    'BankTransfer__BankName'                    = '<<your bank>>'
    'BankTransfer__AccountHolderName'           = '<<your account holder>>'
    'BankTransfer__Iban'                        = '<<your IBAN>>'
    'BankTransfer__AccountNumber'               = '<<your account number>>'
    'OneSignal__AppId'                          = '<<rotate at https://dashboard.onesignal.com>>'
    'OneSignal__RestApiKey'                     = '<<rotate at https://dashboard.onesignal.com>>'
    'OneSignal__DriverAppId'                    = '<<rotate at https://dashboard.onesignal.com>>'
    'OneSignal__DriverRestApiKey'               = '<<rotate at https://dashboard.onesignal.com>>'
    'OneSignal__AdminWebAppId'                  = '<<rotate at https://dashboard.onesignal.com>>'
    'OneSignal__AdminWebRestApiKey'             = '<<rotate at https://dashboard.onesignal.com>>'
    'OneSignal__AdminDefaultWebUrl'             = 'https://your-admin-panel-domain/'
    'OneSignal__DefaultWebUrl'                  = 'https://your-vendor-panel-domain/'
    'DataProtection__KeysPath'                  = '/var/zadana/keys'
}

Write-Host "==== Zadana secrets rotation ====" -ForegroundColor Cyan
Write-Host "Generated symmetric secrets locally; copy provider secrets after rotating them in their dashboards." -ForegroundColor Cyan
Write-Host ""

switch ($Format) {
    'powershell' {
        foreach ($entry in $values.GetEnumerator()) {
            "[Environment]::SetEnvironmentVariable('$($entry.Key)', '$($entry.Value)', 'Machine')"
        }
    }
    'bash' {
        foreach ($entry in $values.GetEnumerator()) {
            "export $($entry.Key)='$($entry.Value)'"
        }
    }
    default {
        foreach ($entry in $values.GetEnumerator()) {
            "$($entry.Key)=$($entry.Value)"
        }
    }
}

Write-Host ""
Write-Host "After applying these env vars, restart the app and verify the build with:" -ForegroundColor Yellow
Write-Host "  dotnet run --project src/Zadana.Api/Zadana.Api.csproj --environment Production" -ForegroundColor Yellow
Write-Host ""
Write-Host "If startup fails, the error message will point to the missing setting." -ForegroundColor Yellow
