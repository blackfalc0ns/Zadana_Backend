# Run as a pre-commit hook (or in CI) to refuse commits that introduce
# obvious secret patterns into the repository.
#
# Hook up locally with:
#   git config core.hooksPath .githooks
#   then create .githooks/pre-commit that calls this script.

$staged = git diff --cached --name-only --diff-filter=ACM 2>$null
if (-not $staged) {
    exit 0
}

$patterns = @(
    @{ Name = 'JWT secret placeholder leak';          Regex = 'SuperSecretKeyForZadanaMarketplace' },
    @{ Name = 'Hard-coded SQL password';              Regex = 'Password=(?!__SET_VIA_ENV__)[^;\"]{6,}' },
    @{ Name = 'Encrypt=False on connection string';   Regex = 'Encrypt\s*=\s*False' },
    @{ Name = 'TrustServerCertificate=True';          Regex = 'TrustServerCertificate\s*=\s*True' },
    @{ Name = 'Resend API key';                       Regex = 're_[A-Za-z0-9]{16,}' },
    @{ Name = 'ImageKit private key';                 Regex = 'private_[A-Za-z0-9+/=]{20,}' },
    @{ Name = 'Moyasar secret key';                   Regex = 'sk_(test|live)_[A-Za-z0-9]{20,}' },
    @{ Name = 'Twilio account SID';                   Regex = 'AC[a-f0-9]{32}' },
    @{ Name = 'AWS access key ID';                    Regex = 'AKIA[0-9A-Z]{16}' },
    @{ Name = 'Generic webhook secret hex 64 chars';  Regex = 'WebhookSecret"\s*:\s*"[a-f0-9]{50,}"' }
)

$hits = @()
foreach ($file in $staged) {
    if (-not (Test-Path $file)) { continue }
    if ($file -match '\.(png|jpg|jpeg|gif|pdf|dll|exe|zip|nupkg)$') { continue }

    $content = Get-Content -Path $file -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    foreach ($p in $patterns) {
        if ($content -match $p.Regex) {
            $hits += [pscustomobject]@{
                File    = $file
                Pattern = $p.Name
                Match   = $matches[0]
            }
        }
    }
}

if ($hits.Count -gt 0) {
    Write-Host "Commit blocked: potential secret detected." -ForegroundColor Red
    $hits | Format-Table -AutoSize | Out-String | Write-Host
    Write-Host "If this is a false positive, bypass with --no-verify after manual review." -ForegroundColor Yellow
    exit 1
}

exit 0
