<#
.SYNOPSIS
  ينظّف git history من الأسرار المسرّبة باستخدام git-filter-repo.

.DESCRIPTION
  ⚠️ هذا السكربت يعيد كتابة كامل تاريخ git. كل من له clone لازم يعيده.
  ⚠️ شغّله مرة واحدة بعد التأكد إن:
     1. كل المطورين دفعوا شغلهم.
     2. أي branches تحت العمل تم merge أو حفظها.
     3. كل التغييرات الحالية تم commit-ها.

.NOTES
  المتطلبات:
    - Python 3 مع git-filter-repo: pip install --user git-filter-repo
    - PowerShell 5.1+ أو PowerShell Core

.EXAMPLE
  pwsh scripts/purge-secrets-from-git.ps1
#>

[CmdletBinding()]
param(
    [switch]$DryRun,
    [string]$BackupRoot = ".."
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

# -------- Pre-flight --------
Write-Host "==== git-filter-repo: purge committed Zadana secrets ====" -ForegroundColor Cyan

$pendingChanges = git status --porcelain
if ($pendingChanges) {
    Write-Host "❌ Working tree is not clean. Commit or stash first." -ForegroundColor Red
    Write-Host $pendingChanges -ForegroundColor Yellow
    exit 1
}

try { python -c "import git_filter_repo" 2>&1 | Out-Null }
catch {
    Write-Host "❌ git-filter-repo Python module not found. Install with:" -ForegroundColor Red
    Write-Host "   python -m pip install --user git-filter-repo" -ForegroundColor Yellow
    exit 1
}

# -------- Backup --------
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = Join-Path $BackupRoot "Zadana-Backend.backup-$timestamp.git"
Write-Host "→ Backing up repo as bare clone to: $backupPath" -ForegroundColor Cyan
git clone --mirror . $backupPath | Out-Null
Write-Host "✅ Backup complete." -ForegroundColor Green

# -------- Build replacements file --------
$secretsFile = "deploy\.secrets-to-purge.txt"
$secretReplacements = @(
    'Password=4Pq!?g7LiD_3==>Password=__REDACTED__',
    'SuperSecretKeyForZadanaMarketplace2026_!!_@@_VeryLongKeyToMeetRequirements==>__REDACTED__',
    're_Aqw1mqeR_EvyaPaFbGs7P3UZQ6qU4VsPm==>__REDACTED__',
    're_GnXKx29R_KsPHcp825RPoNt2qVAXa2jqt==>__REDACTED__',
    'private_I+B7d2/bfoZkFllZCf07835bjb8===>__REDACTED__',
    'public_1bswA0Vq66mBJQlYJxBAyPJm3dE===>__REDACTED__',
    'sk_test_jeibBfSWQV1x7xuiCVZ1UB7ugqkYJ4BEud8dBA2z==>__REDACTED__',
    'sk_test_v6BsG8xqu1UskoPB3ZBTMS9aiT5h3JmCU1yHGtM3==>__REDACTED__',
    'pk_test_RKBfNqBesLkMw4gLcfd8qWMeeu9hxCKMGeYr3Jx1==>__REDACTED__',
    'whsec_Zadana2026MoyasarWebhook_xK9mP4qR7vL2==>__REDACTED__',
    '75865356e4573e2a6fafeed04bcd82c2808bbedcac0b1bad866af5c79e74ac51==>__REDACTED__',
    '212bb8e555cd0f73e2d2fa048afa89469cfa5c14934e3a18d7b4d31f3e72d584==>__REDACTED__',
    'DB23CFF33AC1EE0DD2CB2E0FE8F54E4F==>__REDACTED__',
    'h!8S5dE#T@t7==>__REDACTED__'
)
$secretReplacements | Out-File -FilePath $secretsFile -Encoding utf8
Write-Host "→ Replacements file written: $secretsFile ($($secretReplacements.Count) patterns)" -ForegroundColor Cyan

# -------- Build paths-to-remove file --------
$pathsFile = "deploy\.paths-to-purge.txt"
$pathsToRemove = @(
    'temp_build/',
    '.tmp-build/',
    'temp-build/',
    '.codex-build/',
    '.temp/',
    'tmp-system-logs-api-build/',
    'tmp-system-logs-api-build-2/',
    'tmp-product-card-price-api-build/',
    'tmp-vendor-support-buildDebug/',
    'tmp-vendor-support-testsDebug/',
    'tmp-build/',
    'publish/',
    'test-publish/'
)
$pathsToRemove | Out-File -FilePath $pathsFile -Encoding utf8
Write-Host "→ Paths-to-purge file written: $pathsFile ($($pathsToRemove.Count) paths)" -ForegroundColor Cyan

# -------- Run filter-repo --------
if ($DryRun) {
    Write-Host "→ DRY RUN: filter-repo would replace secrets and remove temp folders." -ForegroundColor Yellow
    exit 0
}

Write-Host "→ Running git-filter-repo (this rewrites history; remote will need force-push)..." -ForegroundColor Cyan
$filterArgs = @(
    '--replace-text', $secretsFile,
    '--invert-paths',
    '--paths-from-file', $pathsFile,
    '--force'
)
python -m git_filter_repo @filterArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ git-filter-repo failed. Restore from backup if needed:" -ForegroundColor Red
    Write-Host "   git clone $backupPath ../Zadana-Backend-restored" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ History rewritten." -ForegroundColor Green

# -------- Verification --------
Write-Host "→ Verifying secrets are gone..." -ForegroundColor Cyan
$leaks = @(
    git log -p --all -S 'SuperSecretKeyForZadanaMarketplace2026' --oneline | Select-Object -First 1
    git log -p --all -S 'private_I+B7d2/bfoZkFllZCf07835bjb8' --oneline | Select-Object -First 1
    git log -p --all -S '4Pq!?g7LiD_3' --oneline | Select-Object -First 1
)
$leaksFound = $leaks | Where-Object { $_ }
if ($leaksFound) {
    Write-Host "⚠️  Some patterns still appear in history:" -ForegroundColor Yellow
    $leaksFound | ForEach-Object { Write-Host "   $_" -ForegroundColor Yellow }
} else {
    Write-Host "✅ No leaks found in rewritten history." -ForegroundColor Green
}

# -------- Re-add origin --------
Write-Host ""
Write-Host "==== Next steps (manual) ====" -ForegroundColor Cyan
Write-Host "1) Re-add the origin remote (filter-repo removes it for safety):" -ForegroundColor Yellow
$originGuess = git -C $backupPath config --get remote.origin.url 2>$null
if ($originGuess) {
    Write-Host "     git remote add origin $originGuess" -ForegroundColor Yellow
} else {
    Write-Host "     git remote add origin <your remote URL>" -ForegroundColor Yellow
}
Write-Host "2) Force push (coordinate with your team first!):" -ForegroundColor Yellow
Write-Host "     git push --force-with-lease --all" -ForegroundColor Yellow
Write-Host "     git push --force-with-lease --tags" -ForegroundColor Yellow
Write-Host "3) Every developer must reclone:" -ForegroundColor Yellow
Write-Host "     rm -rf Zadana-Backend && git clone <repo>" -ForegroundColor Yellow
Write-Host ""
Write-Host "📦 Backup kept at: $backupPath" -ForegroundColor Green
