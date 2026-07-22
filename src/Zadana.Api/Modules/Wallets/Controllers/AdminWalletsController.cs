using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Api.Modules.Wallets.Controllers;

[Route("api/admin/wallets")]
[Tags("Admin Wallet Management API")]
[Authorize(Policy = "AdminOnly")]
public class AdminWalletsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions MoyasarJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("platform-account")]
    [RequireAccess(PermissionKeys.Admin.FinancesManageSettings)]
    public async Task<ActionResult<AdminPlatformBankAccountDto>> GetPlatformAccount(
        [FromServices] IApplicationDbContext context,
        [FromServices] IOptions<BankTransferSettingsOptions> bankTransferSettings,
        [FromServices] IOptions<MoyasarSettings> moyasarSettings,
        CancellationToken cancellationToken = default)
    {
        var account = await context.PlatformBankAccounts
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(account is null
            ? BuildPlatformAccountFallback(bankTransferSettings.Value, moyasarSettings.Value)
            : MapPlatformAccount(account));
    }

    [HttpPut("platform-account")]
    [RequireAccess(true, PermissionKeys.Admin.FinancesManageSettings, PermissionKeys.Admin.FinancesEdit)]
    public async Task<ActionResult<AdminPlatformBankAccountDto>> UpsertPlatformAccount(
        [FromBody] AdminUpsertPlatformBankAccountRequest request,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var account = await context.PlatformBankAccounts
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            account = new PlatformBankAccount(
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.AccountNumber,
                request.CountryCode ?? "SA",
                request.City ?? "Riyadh",
                request.IsBankTransferEnabled,
                request.IsMoyasarPayoutsEnabled,
                request.MoyasarPayoutSourceId,
                request.Notes);
            context.PlatformBankAccounts.Add(account);
        }
        else
        {
            account.Update(
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.AccountNumber,
                request.CountryCode ?? "SA",
                request.City ?? "Riyadh",
                request.IsBankTransferEnabled,
                request.IsMoyasarPayoutsEnabled,
                request.MoyasarPayoutSourceId,
                request.Notes);
        }

        var otherActiveAccounts = await context.PlatformBankAccounts
            .Where(item => item.IsActive && item.Id != account.Id)
            .ToListAsync(cancellationToken);
        foreach (var other in otherActiveAccounts)
        {
            other.Deactivate();
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapPlatformAccount(account));
    }

    [HttpPost("platform-account/moyasar-payout-source")]
    [RequireAccess(true, PermissionKeys.Admin.FinancesManageSettings, PermissionKeys.Admin.FinancesApprove)]
    public async Task<ActionResult<AdminPlatformBankAccountDto>> CreateMoyasarPayoutSource(
        [FromBody] AdminCreateMoyasarPayoutSourceRequest? request,
        [FromServices] IApplicationDbContext context,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IOptions<MoyasarSettings> moyasarSettings,
        CancellationToken cancellationToken = default)
    {
        var settings = moyasarSettings.Value;
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            throw new BusinessRuleException("MOYASAR_SECRET_KEY_REQUIRED", "Moyasar secret key is required before creating payout source account.");
        }

        var account = await context.PlatformBankAccounts
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("PLATFORM_BANK_ACCOUNT_REQUIRED", "Platform bank account is required before enabling Moyasar payouts.");

        var companyCode = FirstNonEmpty(request?.CompanyCode, settings.Payouts.PayoutAccount.CompanyCode);
        var certificate = FirstNonEmpty(request?.Certificate, settings.Payouts.PayoutAccount.Certificate);
        var privateKey = FirstNonEmpty(request?.PrivateKey, settings.Payouts.PayoutAccount.PrivateKey);

        if (string.IsNullOrWhiteSpace(companyCode) ||
            string.IsNullOrWhiteSpace(certificate) ||
            string.IsNullOrWhiteSpace(privateKey))
        {
            throw new BusinessRuleException(
                "MOYASAR_PAYOUT_ACCOUNT_CREDENTIALS_REQUIRED",
                "Moyasar payout account credentials are required: company_code, cert, and key.");
        }

        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.moyasar.com/v1/" : settings.BaseUrl);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.SecretKey + ":")));

        var payload = new
        {
            account_type = "bank",
            properties = new
            {
                iban = account.IBAN
            },
            credentials = new
            {
                company_code = companyCode,
                cert = certificate,
                key = privateKey
            }
        };

        using var response = await httpClient.PostAsJsonAsync("payout_accounts", payload, MoyasarJsonOptions, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException(
                "MOYASAR_PAYOUT_ACCOUNT_CREATE_FAILED",
                $"Moyasar payout account creation failed: {ExtractMoyasarError(rawResponse)}");
        }

        using var document = JsonDocument.Parse(rawResponse);
        var sourceId = document.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new BusinessRuleException("MOYASAR_PAYOUT_ACCOUNT_ID_MISSING", "Moyasar did not return a payout source id.");
        }

        account.Update(
            account.BankName,
            account.AccountHolderName,
            account.IBAN,
            account.AccountNumber,
            account.CountryCode,
            account.City,
            account.IsBankTransferEnabled,
            isMoyasarPayoutsEnabled: true,
            moyasarPayoutSourceId: sourceId,
            account.Notes);

        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapPlatformAccount(account));
    }

    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    public async Task<ActionResult<AdminWalletListDto>> GetWallets(
        [FromServices] IApplicationDbContext context,
        [FromQuery] string? ownerType,
        [FromQuery] Guid? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.Wallets.AsNoTracking().AsQueryable();

        if (ownerId.HasValue && ownerId.Value != Guid.Empty)
        {
            query = query.Where(w => w.OwnerId == ownerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(ownerType) && Enum.TryParse<WalletOwnerType>(ownerType, true, out var type))
        {
            query = query.Where(w => w.OwnerType == type);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var wallets = await query
            .OrderByDescending(w => w.CurrentBalance)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Fetch owner details efficiently
        var vendorIds = wallets.Where(w => w.OwnerType == WalletOwnerType.Vendor).Select(w => w.OwnerId).ToList();
        var driverIds = wallets.Where(w => w.OwnerType == WalletOwnerType.Driver).Select(w => w.OwnerId).ToList();

        var vendors = await context.Vendors.AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id))
            .ToDictionaryAsync(
                v => v.Id,
                v => new
                {
                    Name = !string.IsNullOrWhiteSpace(v.BusinessNameAr)
                        ? v.BusinessNameAr
                        : !string.IsNullOrWhiteSpace(v.BusinessNameEn)
                            ? v.BusinessNameEn
                            : "Unknown Vendor",
                    Phone = v.ContactPhone
                },
                cancellationToken);

        var drivers = await context.Drivers.AsNoTracking()
            .Include(d => d.User)
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(
                d => d.Id,
                d => new { Name = d.User.FullName, Phone = d.User.PhoneNumber ?? "", PayoutDay = d.PayoutDay.ToString() },
                cancellationToken);

        var walletSummaries = await query
            .Select(w => new
            {
                w.OwnerType,
                w.OwnerId,
                w.CurrentBalance,
                w.PendingBalance,
                w.CodOwedBalance
            })
            .ToListAsync(cancellationToken);

        var allVendorIds = walletSummaries
            .Where(w => w.OwnerType == WalletOwnerType.Vendor)
            .Select(w => w.OwnerId)
            .Distinct()
            .ToList();
        var allDriverIds = walletSummaries
            .Where(w => w.OwnerType == WalletOwnerType.Driver)
            .Select(w => w.OwnerId)
            .Distinct()
            .ToList();
        var allVendorHoldTotals = await SumActiveHoldTotalsAsync(context, WalletOwnerType.Vendor, allVendorIds, cancellationToken);
        var allDriverHoldTotals = await SumActiveHoldTotalsAsync(context, WalletOwnerType.Driver, allDriverIds, cancellationToken);

        var totalPlatformBalance = walletSummaries.Sum(w =>
        {
            var activeHolds = w.OwnerType switch
            {
                WalletOwnerType.Vendor => allVendorHoldTotals.GetValueOrDefault(w.OwnerId),
                WalletOwnerType.Driver => allDriverHoldTotals.GetValueOrDefault(w.OwnerId),
                _ => 0m
            };

            return ComputeAvailableBalance(w.OwnerType, w.CurrentBalance, w.PendingBalance, w.CodOwedBalance, activeHolds);
        });
        var totalPendingWithdrawals = walletSummaries.Sum(w => w.PendingBalance);

        var items = wallets.Select(w =>
        {
            string ownerName = "Unknown";
            string ownerPhone = "";
            
            if (w.OwnerType == WalletOwnerType.Vendor && vendors.TryGetValue(w.OwnerId, out var vendor))
            {
                ownerName = vendor.Name;
                ownerPhone = vendor.Phone;
            }
            else if (w.OwnerType == WalletOwnerType.Driver && drivers.TryGetValue(w.OwnerId, out var driver))
            {
                ownerName = driver.Name;
                ownerPhone = driver.Phone;
            }

            var activeHolds = w.OwnerType switch
            {
                WalletOwnerType.Vendor => allVendorHoldTotals.GetValueOrDefault(w.OwnerId),
                WalletOwnerType.Driver => allDriverHoldTotals.GetValueOrDefault(w.OwnerId),
                _ => 0m
            };

            return new AdminWalletSummaryDto(
                w.Id,
                w.OwnerType.ToString(),
                w.OwnerId,
                ownerName,
                ownerPhone,
                w.CurrentBalance,
                w.PendingBalance,
                ComputeAvailableBalance(w.OwnerType, w.CurrentBalance, w.PendingBalance, w.CodOwedBalance, activeHolds),
                w.CodOwedBalance,
                w.CreatedAtUtc
            );
        }).ToList();

        return Ok(new AdminWalletListDto(items, page, pageSize, totalCount, totalPlatformBalance, totalPendingWithdrawals));
    }

    [HttpGet("{id:guid}")]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    public async Task<ActionResult<AdminWalletSummaryDto>> GetWallet(
        Guid id,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var wallet = await context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Wallet", id);

        string ownerName = "Unknown";
        string ownerPhone = "";

        if (wallet.OwnerType == WalletOwnerType.Vendor)
        {
            var vendor = await context.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == wallet.OwnerId, cancellationToken);
            ownerName = vendor is null
                ? "Unknown Vendor"
                : !string.IsNullOrWhiteSpace(vendor.BusinessNameAr)
                    ? vendor.BusinessNameAr
                    : !string.IsNullOrWhiteSpace(vendor.BusinessNameEn)
                        ? vendor.BusinessNameEn
                        : "Unknown Vendor";
            ownerPhone = vendor?.ContactPhone ?? string.Empty;
        }
        else if (wallet.OwnerType == WalletOwnerType.Driver)
        {
            var driver = await context.Drivers.AsNoTracking().Include(d => d.User).FirstOrDefaultAsync(d => d.Id == wallet.OwnerId, cancellationToken);
            ownerName = driver?.User.FullName ?? "Unknown Driver";
            ownerPhone = driver?.User.PhoneNumber ?? "";
        }

        var activeHolds = await SumActiveHoldTotalsAsync(
            context,
            wallet.OwnerType,
            [wallet.OwnerId],
            cancellationToken);

        return Ok(new AdminWalletSummaryDto(
            wallet.Id,
            wallet.OwnerType.ToString(),
            wallet.OwnerId,
            ownerName,
            ownerPhone,
            wallet.CurrentBalance,
            wallet.PendingBalance,
            ComputeAvailableBalance(wallet.OwnerType, wallet.CurrentBalance, wallet.PendingBalance, wallet.CodOwedBalance, activeHolds.GetValueOrDefault(wallet.OwnerId)),
            wallet.CodOwedBalance,
            wallet.CreatedAtUtc
        ));
    }

    [HttpGet("{id:guid}/transactions")]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    public async Task<ActionResult<AdminWalletTransactionListDto>> GetTransactions(
        Guid id,
        [FromServices] IApplicationDbContext context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.WalletTransactions.AsNoTracking().Where(t => t.WalletId == id);
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AdminWalletTransactionDto(
                t.Id,
                t.TxnType.ToString(),
                t.Direction,
                t.Amount,
                t.Description,
                t.ReferenceType,
                t.ReferenceId.HasValue ? t.ReferenceId.Value.ToString() : null,
                t.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Ok(new AdminWalletTransactionListDto(items, page, pageSize, totalCount));
    }

    [HttpPost("{id:guid}/adjustments")]
    [RequireAccess(PermissionKeys.Admin.FinancesEdit)]
    public async Task<ActionResult<AdminWalletTransactionDto>> CreateAdjustment(
        Guid id,
        [FromBody] AdminCreateAdjustmentRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] FinancialEventPostingService financialEventPostingService,
        [FromServices] WalletProjectionUpdater walletProjectionUpdater,
        [FromServices] IDriverWalletNotificationService driverWalletNotificationService,
        CancellationToken cancellationToken = default)
    {
        var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Wallet", id);

        var ownerType = wallet.OwnerType switch
        {
            WalletOwnerType.Vendor => FinancialOwnerType.Vendor,
            WalletOwnerType.Driver => FinancialOwnerType.Driver,
            WalletOwnerType.Platform => FinancialOwnerType.Platform,
            _ => throw new BusinessRuleException("INVALID_WALLET_OWNER", "Unsupported wallet owner type.")
        };

        var ownerDebit = request.Direction == "OUT" ? request.Amount : 0m;
        var ownerCredit = request.Direction == "IN" ? request.Amount : 0m;
        var offsetDebit = request.Direction == "IN" ? request.Amount : 0m;
        var offsetCredit = request.Direction == "OUT" ? request.Amount : 0m;

        var postingResult = await financialEventPostingService.PostAsync(
            FinancialEventType.FinancialAdjustmentApplied,
            $"admin-adjustment:{wallet.Id:N}:{Guid.NewGuid():N}",
            [
                new JournalLineDraft(
                    FinancialAccountCode.ManualAdjustment,
                    offsetDebit,
                    offsetCredit,
                    Memo: $"Offset for admin wallet adjustment {wallet.Id}"),
                new JournalLineDraft(
                    FinancialAccountCode.ManualAdjustment,
                    ownerDebit,
                    ownerCredit,
                    ownerType,
                    wallet.OwnerId,
                    Memo: request.Description)
            ],
            description: request.Description,
            cancellationToken: cancellationToken);

        await walletProjectionUpdater.ApplyJournalEntryAsync(postingResult.JournalEntryId, cancellationToken);

        var txn = await context.WalletTransactions
            .AsNoTracking()
            .Where(item => item.WalletId == wallet.Id && item.ReferenceType == "JournalLine")
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstAsync(cancellationToken);

        if (wallet.OwnerType == WalletOwnerType.Driver)
        {
            var driverUserId = await context.Drivers
                .AsNoTracking()
                .Where(driver => driver.Id == wallet.OwnerId)
                .Select(driver => driver.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (driverUserId != Guid.Empty)
            {
                await driverWalletNotificationService.NotifyAdminWalletAdjustmentAsync(
                    driverUserId,
                    wallet.Id,
                    txn.Id,
                    txn.Amount,
                    txn.Direction,
                    cancellationToken);
            }
        }

        return Ok(new AdminWalletTransactionDto(
            txn.Id,
            txn.TxnType.ToString(),
            txn.Direction,
            txn.Amount,
            txn.Description,
            txn.ReferenceType,
            txn.ReferenceId.HasValue ? txn.ReferenceId.Value.ToString() : null,
            txn.CreatedAtUtc
        ));
    }

    [HttpGet("withdrawals")]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    public async Task<ActionResult<AdminWithdrawalRequestListDto>> GetWithdrawals(
        [FromServices] IApplicationDbContext context,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.DriverWithdrawalRequests.AsNoTracking()
            .Include(w => w.DriverPayoutMethod)
            .Include(w => w.Payout)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Zadana.Domain.Modules.Wallets.Enums.DriverWithdrawalStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(w => w.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var withdrawals = await query
            .OrderByDescending(w => w.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var driverIds = withdrawals.Select(w => w.DriverId).Distinct().ToList();
        var drivers = await context.Drivers.AsNoTracking()
            .Include(d => d.User)
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(
                d => d.Id,
                d => new { Name = d.User.FullName, Phone = d.User.PhoneNumber ?? "", PayoutDay = d.PayoutDay.ToString() },
                cancellationToken);

        var items = withdrawals.Select(w =>
        {
            var driverInfo = drivers.GetValueOrDefault(w.DriverId);
            return new AdminDriverWithdrawalRequestDto(
                w.Id,
                w.DriverId,
                driverInfo?.Name ?? "Unknown",
                driverInfo?.Phone ?? "",
                w.Amount,
                w.Status.ToString(),
                w.TransferReference,
                w.FailureReason,
                w.CreatedAtUtc,
                w.ProcessedAtUtc,
                new AdminDriverPayoutMethodDto(
                    w.DriverPayoutMethod.Id,
                    w.DriverPayoutMethod.MethodType.ToString(),
                    w.DriverPayoutMethod.AccountHolderName,
                    w.DriverPayoutMethod.ProviderName ?? string.Empty,
                    w.DriverPayoutMethod.MaskedLabel
                ),
                w.PayoutId,
                w.Payout?.ProviderName,
                w.Payout?.ProviderTransferId,
                w.RequestedPayoutDay?.ToString() ?? driverInfo?.PayoutDay,
                w.ReviewedByUserId,
                w.ReviewedAtUtc
            );
        }).ToList();

        return Ok(new AdminWithdrawalRequestListDto(items, page, pageSize, totalCount));
    }

    [HttpPost("withdrawals/{id:guid}/process")]
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
    public async Task<ActionResult<AdminProcessWithdrawalResultDto>> ProcessWithdrawal(
        Guid id,
        [FromBody] AdminProcessWithdrawalRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] FinancialEventPostingService financialEventPostingService,
        [FromServices] WalletProjectionUpdater walletProjectionUpdater,
        [FromServices] IOptions<FinancialSettingsOptions> financialSettings,
        [FromServices] IDriverWalletNotificationService driverWalletNotificationService,
        CancellationToken cancellationToken = default,
        [FromServices] ICurrentUserService? currentUserService = null,
        [FromServices] PayoutOrchestrator? payoutOrchestrator = null,
        [FromServices] IOptions<MoyasarSettings>? moyasarSettings = null,
        [FromServices] ISettlementProcessingSettingsService? settlementProcessingSettingsService = null)
    {
        var reviewingAdminId = currentUserService?.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");
        var withdrawal = await context.DriverWithdrawalRequests
            .Include(w => w.Wallet)
            .Include(w => w.DriverPayoutMethod)
            .Include(w => w.Payout)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("DriverWithdrawalRequest", id);

        if (withdrawal.Status != Zadana.Domain.Modules.Wallets.Enums.DriverWithdrawalStatus.Pending && 
            withdrawal.Status != Zadana.Domain.Modules.Wallets.Enums.DriverWithdrawalStatus.Processing)
        {
            throw new BusinessRuleException("INVALID_STATUS", "Only pending or processing withdrawals can be processed.");
        }

        Payout? payout = withdrawal.Payout;
        var isAutomaticProcessing = settlementProcessingSettingsService is null ||
            await settlementProcessingSettingsService.IsAutomaticAsync(cancellationToken);
        var shouldNotifyDriver = false;

        if (request.IsApproved)
        {
            var currentPayoutDay = await context.Drivers
                .AsNoTracking()
                .Where(driver => driver.Id == withdrawal.DriverId)
                .Select(driver => (PayoutScheduleDay?)driver.PayoutDay)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Driver", withdrawal.DriverId);
            var payoutDay = withdrawal.RequestedPayoutDay ?? currentPayoutDay;

            var enabledPayoutDays = settlementProcessingSettingsService is null
                ? PayoutScheduleDayPolicy.DefaultPayoutDays
                : await settlementProcessingSettingsService.GetEnabledPayoutDaysAsync(cancellationToken);
            if (!enabledPayoutDays.Contains(payoutDay))
            {
                throw new BusinessRuleException(
                    "PAYOUT_DAY_DISABLED",
                    "This driver's selected payout day is not enabled by the platform.");
            }

            if (!PayoutScheduleDayPolicy.IsPayoutDay(SaudiTime.Today, payoutDay))
            {
                throw new BusinessRuleException(
                    "DRIVER_WITHDRAWAL_NOT_DUE",
                    $"This driver selected {payoutDay}; withdrawals can only be paid on that day.");
            }

            if (withdrawal.DriverPayoutMethod.MethodType != DriverPayoutMethodType.BankAccount)
            {
                throw new BusinessRuleException("DRIVER_BANK_ACCOUNT_REQUIRED", "Only bank account withdrawal methods can be paid through bank transfer.");
            }

            if (!IsValidSaudiIban(withdrawal.DriverPayoutMethod.AccountIdentifier))
            {
                throw new BusinessRuleException("DRIVER_BANK_IBAN_INVALID", "Driver bank account must be a valid Saudi IBAN.");
            }

            withdrawal.RecordApproval(reviewingAdminId);

            // Preparing a manual transfer deliberately does not mark it paid
            // and never submits it to a gateway. The returned PayoutId is used
            // later with /api/admin/payouts/{id}/confirm-manual after the bank
            // reference and proof have been captured.
            if (!isAutomaticProcessing)
            {
                var preparation = await EnsureDriverWithdrawalPreparedAsync(
                    context,
                    withdrawal,
                    payoutDay,
                    cancellationToken);
                withdrawal = preparation.Withdrawal;
                payout = preparation.Payout;
                shouldNotifyDriver = preparation.PreparedNow;
            }
            else
            {
                if (payoutOrchestrator is null)
                {
                    throw new BusinessRuleException("PAYOUT_ORCHESTRATOR_REQUIRED", "Payout orchestration service is required.");
                }

                var hasConfiguredPayoutGateway = payoutOrchestrator.HasEnabledGateway &&
                    (moyasarSettings is null ||
                     await HasConfiguredPayoutSourceAsync(context, moyasarSettings.Value, cancellationToken));

                // A free-form transfer reference is never a valid automatic
                // payment result. If money will leave outside the platform,
                // finance must use Manual mode and confirm it with evidence.
                if (!hasConfiguredPayoutGateway)
                {
                    throw new BusinessRuleException(
                        "PAYOUT_GATEWAY_UNAVAILABLE",
                        "No automatic payout gateway is configured. Switch settlement processing to Manual, prepare the withdrawal, then confirm it with a bank reference and transfer proof.");
                }

                var preparation = await EnsureDriverWithdrawalPreparedAsync(
                    context,
                    withdrawal,
                    payoutDay,
                    cancellationToken);
                withdrawal = preparation.Withdrawal;
                payout = preparation.Payout;
                shouldNotifyDriver = preparation.PreparedNow;
                payout = await payoutOrchestrator.TriggerAsync(
                    payout.Id,
                    reviewingAdminId,
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            var rejectionReason = request.FailureReason ?? "Rejected by admin";
            withdrawal.RecordRejection(reviewingAdminId, rejectionReason);
            if (withdrawal.PayoutId.HasValue)
            {
                if (payoutOrchestrator is null)
                {
                    throw new BusinessRuleException(
                        "PAYOUT_ORCHESTRATOR_REQUIRED",
                        "Payout orchestration service is required to cancel a prepared withdrawal.");
                }

                // Do not leave a pending manual payout behind when finance
                // rejects a request after it was prepared. CancelAsync also
                // releases the active claim/hold when safe, and deliberately
                // refuses a submitted or in-flight transfer that must instead
                // be reconciled.
                await payoutOrchestrator.CancelAsync(
                    withdrawal.PayoutId.Value,
                    reviewingAdminId,
                    cancellationToken);
                payout = await context.Payouts
                    .FirstAsync(item => item.Id == withdrawal.PayoutId.Value, cancellationToken);
            }
            else
            {
                await CancelDriverWithdrawalHoldsAsync(context, withdrawal, rejectionReason, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            shouldNotifyDriver = true;
        }

        var driverUserId = await context.Drivers
            .AsNoTracking()
            .Where(driver => driver.Id == withdrawal.DriverId)
            .Select(driver => driver.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (shouldNotifyDriver && driverUserId != Guid.Empty)
        {
            if (!request.IsApproved)
            {
                await driverWalletNotificationService.NotifyWithdrawalRejectedAsync(
                    driverUserId,
                    withdrawal,
                    cancellationToken);
            }
        }

        var manualWorkflowRequired = request.IsApproved &&
            !isAutomaticProcessing &&
            payout is not null &&
            payout.Status != PayoutStatus.Paid;

        return Ok(new AdminProcessWithdrawalResultDto(
            withdrawal.Id,
            withdrawal.Status.ToString(),
            payout?.Id ?? withdrawal.PayoutId,
            payout?.Status.ToString(),
            manualWorkflowRequired,
            manualWorkflowRequired && payout is not null
                ? $"/api/admin/payouts/{payout.Id}/manual-claim"
                : null,
            manualWorkflowRequired && payout is not null
                ? $"/api/admin/payouts/{payout.Id}/manual-bank-submission"
                : null,
            manualWorkflowRequired && payout is not null
                ? $"/api/admin/payouts/{payout.Id}/confirm-manual"
                : null,
            withdrawal.TransferReference,
            withdrawal.FailureReason));
    }

    private sealed record DriverWithdrawalPreparation(
        DriverWithdrawalRequest Withdrawal,
        Payout Payout,
        bool PreparedNow);

    /// <summary>
    /// Prepares a driver withdrawal for gateway or manual settlement. New
    /// settlement, payout, withdrawal link, processing state and active hold
    /// are persisted together by one SaveChanges call. Relational providers
    /// wrap that call in a transaction, and the withdrawal concurrency tokens
    /// make a concurrent admin request resolve to the same linked payout.
    /// </summary>
    private static async Task<DriverWithdrawalPreparation> EnsureDriverWithdrawalPreparedAsync(
        IApplicationDbContext context,
        DriverWithdrawalRequest withdrawal,
        PayoutScheduleDay payoutDay,
        CancellationToken cancellationToken)
    {
        if (withdrawal.PayoutId.HasValue)
        {
            var existingPayout = withdrawal.Payout ?? await context.Payouts
                .FirstOrDefaultAsync(item => item.Id == withdrawal.PayoutId.Value, cancellationToken)
                ?? throw new BusinessRuleException(
                    "WITHDRAWAL_PAYOUT_NOT_FOUND",
                    "The withdrawal is linked to a payout that no longer exists and requires finance review.");

            if (existingPayout.Status == PayoutStatus.Cancelled)
            {
                throw new BusinessRuleException(
                    "WITHDRAWAL_PAYOUT_CANCELLED",
                    "A cancelled payout cannot be processed again. Create a new withdrawal request after finance review.");
            }

            var changed = false;
            if (!existingPayout.ScheduledPayoutDay.HasValue)
            {
                // Legacy prepared withdrawals did not persist a schedule. The
                // first safe preparation captures the day selected for this
                // withdrawal so later profile edits cannot move it.
                existingPayout.SetScheduledPayoutDay(payoutDay);
                changed = true;
            }

            if (existingPayout.Status != PayoutStatus.Paid)
            {
                changed |= await EnsureDriverWithdrawalHoldAsync(context, withdrawal, cancellationToken);
            }

            if (changed)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            return new DriverWithdrawalPreparation(withdrawal, existingPayout, changed);
        }

        var settlement = new Settlement(null, withdrawal.DriverId);
        settlement.UpdateTotals(withdrawal.Amount, 0m);
        settlement.Approve();

        var payout = new Payout(settlement.Id, withdrawal.Amount);
        payout.SetScheduledPayoutDay(payoutDay);
        // The withdrawal method can be edited after this request is prepared.
        // Persist the complete immutable recipient snapshot (encrypted at rest)
        // instead of looking the method up again at payment time.
        payout.PrepareDestination(
            PayoutDestinationType.DriverPayoutMethod,
            withdrawal.DestinationSnapshot ??
            PayoutDestinationSnapshotCodec.CreateDriverPayoutMethod(withdrawal.DriverPayoutMethod));

        withdrawal.LinkPayout(payout.Id);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await EnsureDriverWithdrawalHoldAsync(context, withdrawal, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new DriverWithdrawalPreparation(withdrawal, payout, PreparedNow: true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReloadConcurrentPreparationAsync(context, withdrawal.Id, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique hold idempotency key may win a race before the
            // optimistic concurrency update. It is idempotent only if another
            // transaction actually linked a payout to this withdrawal.
            return await ReloadConcurrentPreparationAsync(context, withdrawal.Id, cancellationToken);
        }
    }

    private static async Task<DriverWithdrawalPreparation> ReloadConcurrentPreparationAsync(
        IApplicationDbContext context,
        Guid withdrawalId,
        CancellationToken cancellationToken)
    {
        if (context is DbContext dbContext)
        {
            // Failed inserts and the failed PayoutId update remain tracked
            // after SaveChanges throws. Clear them before loading the winner
            // so no later action in this request can write stale entities.
            dbContext.ChangeTracker.Clear();
        }

        var winner = await context.DriverWithdrawalRequests
            .Include(item => item.Payout)
            .FirstOrDefaultAsync(item => item.Id == withdrawalId, cancellationToken);

        if (winner?.PayoutId is not { } || winner.Payout is null)
        {
            throw new BusinessRuleException(
                "WITHDRAWAL_PROCESSING_CONFLICT",
                "The withdrawal changed while it was being prepared. Refresh it and try again.");
        }

        return new DriverWithdrawalPreparation(winner, winner.Payout, PreparedNow: false);
    }

    private static AdminPlatformBankAccountDto BuildPlatformAccountFallback(
        BankTransferSettingsOptions bankTransfer,
        MoyasarSettings moyasar)
    {
        var canReceive = bankTransfer.Enabled &&
            (!string.IsNullOrWhiteSpace(bankTransfer.Iban) || !string.IsNullOrWhiteSpace(bankTransfer.AccountNumber));
        var canSend = moyasar.Payouts.Enabled && !string.IsNullOrWhiteSpace(moyasar.Payouts.SourceId);

        return new AdminPlatformBankAccountDto(
            null,
            bankTransfer.BankName,
            bankTransfer.AccountHolderName,
            bankTransfer.Iban,
            bankTransfer.AccountNumber,
            moyasar.Payouts.DefaultCountry,
            moyasar.Payouts.DefaultCity,
            false,
            bankTransfer.Enabled,
            moyasar.Payouts.Enabled,
            moyasar.Payouts.SourceId,
            "Loaded from appsettings fallback. Save this form to manage the platform account from database.",
            null,
            canReceive,
            canSend);
    }

    private static AdminPlatformBankAccountDto MapPlatformAccount(PlatformBankAccount account)
    {
        var canReceive = account.IsActive &&
            account.IsBankTransferEnabled &&
            (!string.IsNullOrWhiteSpace(account.IBAN) || !string.IsNullOrWhiteSpace(account.AccountNumber));
        var canSend = account.IsActive &&
            account.IsMoyasarPayoutsEnabled &&
            !string.IsNullOrWhiteSpace(account.MoyasarPayoutSourceId);

        return new AdminPlatformBankAccountDto(
            account.Id,
            account.BankName,
            account.AccountHolderName,
            account.IBAN,
            account.AccountNumber,
            account.CountryCode,
            account.City,
            account.IsActive,
            account.IsBankTransferEnabled,
            account.IsMoyasarPayoutsEnabled,
            account.MoyasarPayoutSourceId,
            account.Notes,
            account.UpdatedAtUtc,
            canReceive,
            canSend);
    }

    private static async Task<bool> HasConfiguredPayoutSourceAsync(
        IApplicationDbContext context,
        MoyasarSettings moyasarSettings,
        CancellationToken cancellationToken)
    {
        if (await context.PlatformBankAccounts
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.IsActive &&
                        item.IsMoyasarPayoutsEnabled &&
                        item.MoyasarPayoutSourceId != null &&
                        item.MoyasarPayoutSourceId != string.Empty,
                    cancellationToken))
        {
            return true;
        }

        return moyasarSettings.Payouts.Enabled && !string.IsNullOrWhiteSpace(moyasarSettings.Payouts.SourceId);
    }

    private static async Task<bool> EnsureDriverWithdrawalHoldAsync(
        IApplicationDbContext context,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken)
    {
        var exists = await context.WalletHolds.AnyAsync(
            item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == withdrawal.DriverId &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.ReferenceType == "DriverWithdrawalRequest" &&
                item.ReferenceId == withdrawal.Id &&
                item.Status == WalletHoldStatus.Active,
            cancellationToken);

        if (exists)
        {
            return false;
        }

        var activeHolds = await context.WalletHolds
            .AsNoTracking()
            .Where(item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == withdrawal.DriverId &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.Status == WalletHoldStatus.Active)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var wallet = withdrawal.Wallet ??
            await context.Wallets.FirstAsync(item => item.Id == withdrawal.WalletId, cancellationToken);
        var available = wallet.CurrentBalance - wallet.CodOwedBalance - wallet.PendingBalance - activeHolds;
        if (available < withdrawal.Amount)
        {
            throw new BusinessRuleException("INSUFFICIENT_WITHDRAWABLE_BALANCE", "Withdrawal amount exceeds available balance.");
        }

        context.WalletHolds.Add(new WalletHold(
            WalletOwnerType.Driver,
            withdrawal.DriverId,
            withdrawal.Amount,
            WalletHoldReason.Withdrawal,
            $"driver-withdrawal:{withdrawal.Id:N}",
            walletId: wallet.Id,
            referenceType: "DriverWithdrawalRequest",
            referenceId: withdrawal.Id,
            memo: "Driver withdrawal approved for transfer"));

        return true;
    }

    private static async Task CancelDriverWithdrawalHoldsAsync(
        IApplicationDbContext context,
        DriverWithdrawalRequest withdrawal,
        string reason,
        CancellationToken cancellationToken)
    {
        var holds = await context.WalletHolds
            .Where(item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == withdrawal.DriverId &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.Status == WalletHoldStatus.Active &&
                item.ReferenceType == "DriverWithdrawalRequest" &&
                item.ReferenceId == withdrawal.Id)
            .ToListAsync(cancellationToken);

        foreach (var hold in holds)
        {
            hold.Cancel(reason);
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static decimal ComputeAvailableBalance(Wallet wallet, decimal activeHolds) =>
        ComputeAvailableBalance(wallet.OwnerType, wallet.CurrentBalance, wallet.PendingBalance, wallet.CodOwedBalance, activeHolds);

    private static decimal ComputeAvailableBalance(
        WalletOwnerType ownerType,
        decimal currentBalance,
        decimal pendingBalance,
        decimal codOwedBalance,
        decimal activeHolds)
    {
        var reserved = pendingBalance + activeHolds;
        if (ownerType == WalletOwnerType.Driver)
        {
            return Math.Max(0m, currentBalance - codOwedBalance - reserved);
        }

        return Math.Max(0m, currentBalance - reserved);
    }

    private static async Task<Dictionary<Guid, decimal>> SumActiveHoldTotalsAsync(
        IApplicationDbContext context,
        WalletOwnerType ownerType,
        IReadOnlyList<Guid> ownerIds,
        CancellationToken cancellationToken)
    {
        if (ownerIds.Count == 0)
        {
            return [];
        }

        return await context.WalletHolds
            .AsNoTracking()
            .Where(hold =>
                hold.OwnerType == ownerType &&
                ownerIds.Contains(hold.OwnerId) &&
                hold.Status == WalletHoldStatus.Active)
            .GroupBy(hold => hold.OwnerId)
            .Select(group => new { group.Key, Total = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.Key, item => item.Total, cancellationToken);
    }

    private static bool IsValidSaudiIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length == 24 &&
            clean.StartsWith("SA", StringComparison.OrdinalIgnoreCase) &&
            clean.Skip(2).All(char.IsDigit);
    }

    private static string ExtractMoyasarError(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return "empty provider response";
        }

        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            if (document.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!;
            }

            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind != JsonValueKind.Null)
            {
                return errors.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return rawResponse.Length <= 500 ? rawResponse : rawResponse[..500];
    }
}
