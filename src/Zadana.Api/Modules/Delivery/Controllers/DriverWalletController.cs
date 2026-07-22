using System.Data;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Api.Modules.Finances.Services;
using Zadana.Api.Security;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Application.Modules.Wallets.Interfaces;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers/wallet")]
[Tags("Driver App API")]
[Authorize(Policy = "DriverOnly")]
public class DriverWalletController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DriverWalletSummaryDto>> GetWallet(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverWalletReadService driverWalletReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        return Ok(await driverWalletReadService.GetWalletSummaryAsync(userId, cancellationToken));
    }

    [HttpGet("payout-preference")]
    public async Task<ActionResult<DriverPayoutPreferenceDto>> GetPayoutPreference(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] ISettlementProcessingSettingsService settlementProcessingSettingsService,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        return Ok(await ToPayoutPreferenceDtoAsync(driver, settlementProcessingSettingsService, cancellationToken));
    }

    [HttpPut("payout-preference")]
    public async Task<ActionResult<DriverPayoutPreferenceDto>> UpdatePayoutPreference(
        [FromBody] UpdateDriverPayoutPreferenceRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] ISettlementProcessingSettingsService settlementProcessingSettingsService,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !PayoutScheduleDayPolicy.TryParse(request.PayoutDay, out var payoutDay))
        {
            throw new BadRequestException(
                "INVALID_PAYOUT_DAY",
                "Payout day must be a valid day of the week.");
        }

        await settlementProcessingSettingsService.EnsurePayoutDayEnabledAsync(
            payoutDay,
            cancellationToken);

        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        driver.UpdatePayoutDay(payoutDay);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(await ToPayoutPreferenceDtoAsync(driver, settlementProcessingSettingsService, cancellationToken));
    }

    [HttpGet("withdrawal-settings")]
    public async Task<ActionResult<DriverWithdrawalSettingsDto>> GetWithdrawalSettings(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] ISettlementProcessingSettingsService settlementProcessingSettingsService,
        [FromServices] IOptions<FinancialSettingsOptions> financialSettings,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var requestsCreatedToday = await context.DriverWithdrawalRequests
            .AsNoTracking()
            .CountAsync(
                item => item.DriverId == driver.Id &&
                        item.CreatedAtUtc >= SaudiTime.StartOfTodayUtc &&
                        item.CreatedAtUtc < SaudiTime.StartOfTomorrowUtc,
                cancellationToken);
        var hasActiveWithdrawal = await context.DriverWithdrawalRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.DriverId == driver.Id &&
                        (item.Status == DriverWithdrawalStatus.Pending ||
                         item.Status == DriverWithdrawalStatus.Processing),
                cancellationToken);
        var enabledDays = await settlementProcessingSettingsService
            .GetEnabledPayoutDaysAsync(cancellationToken);
        var limits = financialSettings.Value;

        return Ok(new DriverWithdrawalSettingsDto(
            limits.DriverMinimumWithdrawalAmount,
            limits.DriverMaximumWithdrawalAmount,
            limits.DriverMaximumWithdrawalRequestsPerDay,
            requestsCreatedToday,
            hasActiveWithdrawal,
            CurrencyPolicy.OfficialCurrency,
            driver.PayoutDay.ToString(),
            enabledDays.Select(day => day.ToString()).ToArray()));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<DriverWalletTransactionListDto>> GetTransactions(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(context, driver.Id, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapTransaction())
            .ToListAsync(cancellationToken);

        return Ok(new DriverWalletTransactionListDto(items, page, pageSize, totalCount));
    }

    [HttpGet("payment-methods")]
    public async Task<ActionResult<IReadOnlyList<DriverPayoutMethodDto>>> GetPaymentMethods(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var methods = await context.DriverPayoutMethods
            .AsNoTracking()
            .Where(m => m.DriverId == driver.Id)
            .OrderByDescending(m => m.IsPrimary)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Select(MapPayoutMethod())
            .ToListAsync(cancellationToken);

        return Ok(methods);
    }

    [HttpPost("payment-methods")]
    public async Task<ActionResult<DriverPayoutMethodDto>> CreatePaymentMethod(
        [FromBody] CreateDriverPayoutMethodRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        CancellationToken cancellationToken = default)
    {
        EnsurePayoutMethodRequest(request);
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var methodType = ParseMethodType(request!.Type);
        EnsureSupportedBankPayoutMethod(methodType, request.AccountIdentifier);

        var approvalRequestId = await profileChangeApprovalService.SubmitAsync(
            driver.UserId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverPayoutMethodCreate,
            $"Driver {GetDriverDisplayName(driver)} requested payout method creation.",
            new DriverPayoutMethodCreatePayload(
                driver.Id,
                request.Type,
                request.AccountHolderName,
                request.AccountIdentifier,
                request.ProviderName,
                request.IsPrimary),
            BuildDriverPayoutMethodApprovalAlert(driver, "create"),
            cancellationToken);

        return Accepted(new
        {
            approvalRequestId,
            message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_PAYOUT_METHOD_CHANGE_PENDING_APPROVAL")
        });
    }

    [HttpPut("payment-methods/{id:guid}")]
    public async Task<ActionResult<DriverPayoutMethodDto>> UpdatePaymentMethod(
        Guid id,
        [FromBody] UpdateDriverPayoutMethodRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        CancellationToken cancellationToken = default)
    {
        EnsurePayoutMethodRequest(request);
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var payoutMethod = await context.DriverPayoutMethods
            .FirstOrDefaultAsync(m => m.Id == id && m.DriverId == driver.Id, cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", id);

        var methodType = ParseMethodType(request!.Type);
        EnsureSupportedBankPayoutMethod(methodType, request.AccountIdentifier);

        var approvalRequestId = await profileChangeApprovalService.SubmitAsync(
            driver.UserId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverPayoutMethodUpdate,
            $"Driver {GetDriverDisplayName(driver)} requested payout method updates.",
            new DriverPayoutMethodUpdatePayload(
                driver.Id,
                payoutMethod.Id,
                request.Type,
                request.AccountHolderName,
                request.AccountIdentifier,
                request.ProviderName),
            BuildDriverPayoutMethodApprovalAlert(driver, "update", payoutMethod.Id),
            cancellationToken);

        return Accepted(new
        {
            approvalRequestId,
            message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_PAYOUT_METHOD_CHANGE_PENDING_APPROVAL")
        });
    }

    [HttpDelete("payment-methods/{id:guid}")]
    public async Task<IActionResult> DeletePaymentMethod(
        Guid id,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var payoutMethod = await context.DriverPayoutMethods
            .FirstOrDefaultAsync(m => m.Id == id && m.DriverId == driver.Id, cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", id);

        var hasWithdrawalHistory = await context.DriverWithdrawalRequests
            .AnyAsync(w => w.DriverPayoutMethodId == id, cancellationToken);

        if (hasWithdrawalHistory)
        {
            throw new BusinessRuleException(
                "DRIVER_PAYOUT_METHOD_IN_USE",
                "ما تقدر تحذف طريقة السحب لأنها مرتبطة بطلبات سحب سابقة أو حالية | This payout method cannot be deleted because it is linked to withdrawal requests.");
        }

        var approvalRequestId = await profileChangeApprovalService.SubmitAsync(
            driver.UserId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverPayoutMethodDelete,
            $"Driver {GetDriverDisplayName(driver)} requested payout method deletion.",
            new DriverPayoutMethodDeletePayload(driver.Id, payoutMethod.Id),
            BuildDriverPayoutMethodApprovalAlert(driver, "delete", payoutMethod.Id),
            cancellationToken);

        return Accepted(new
        {
            approvalRequestId,
            message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_PAYOUT_METHOD_DELETION_PENDING_APPROVAL")
        });
    }

    [HttpPost("payment-methods/{id:guid}/make-primary")]
    public async Task<ActionResult<DriverPayoutMethodDto>> MakePrimary(
        Guid id,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var payoutMethod = await context.DriverPayoutMethods
            .FirstOrDefaultAsync(m => m.Id == id && m.DriverId == driver.Id, cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", id);

        EnsureSupportedBankPayoutMethod(payoutMethod.MethodType, payoutMethod.AccountIdentifier);

        var approvalRequestId = await profileChangeApprovalService.SubmitAsync(
            driver.UserId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverPayoutMethodMakePrimary,
            $"Driver {GetDriverDisplayName(driver)} requested a primary payout method change.",
            new DriverPayoutMethodMakePrimaryPayload(driver.Id, payoutMethod.Id),
            BuildDriverPayoutMethodApprovalAlert(driver, "make_primary", payoutMethod.Id),
            cancellationToken);

        return Accepted(new
        {
            approvalRequestId,
            message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_PAYOUT_METHOD_CHANGE_PENDING_APPROVAL")
        });
    }

    [HttpPost("withdrawals")]
    [EnableRateLimiting(RateLimitPolicyNames.WalletMutations)]
    public async Task<ActionResult<DriverWithdrawalRequestDto>> CreateWithdrawal(
        [FromBody] CreateDriverWithdrawalRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDriverWalletNotificationService driverWalletNotificationService,
        [FromServices] IAdminAlertService adminAlertService,
        [FromServices] ILogger<DriverWalletController> logger,
        CancellationToken cancellationToken = default,
        [FromServices] IOptions<FinancialSettingsOptions>? financialSettings = null)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var limits = financialSettings?.Value ?? new FinancialSettingsOptions();
        if (request.Amount < limits.DriverMinimumWithdrawalAmount ||
            request.Amount > limits.DriverMaximumWithdrawalAmount)
        {
            throw new BadRequestException(
                "INVALID_WITHDRAWAL_AMOUNT",
                $"Withdrawal amount must be between {limits.DriverMinimumWithdrawalAmount:0.##} and {limits.DriverMaximumWithdrawalAmount:0.##} SAR.");
        }

        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);

        DriverPayoutMethod? payoutMethod;
        if (request.PaymentMethodId.HasValue)
        {
            payoutMethod = await context.DriverPayoutMethods.FirstOrDefaultAsync(
                m => m.Id == request.PaymentMethodId.Value && m.DriverId == driver.Id,
                cancellationToken);

            if (payoutMethod is null)
            {
                throw new BusinessRuleException(
                    "DRIVER_PAYOUT_METHOD_NOT_FOUND",
                    "طريقة السحب المحددة غير موجودة لهذا المندوب | The selected payout method was not found for this driver.");
            }
        }
        else
        {
            payoutMethod = await context.DriverPayoutMethods.FirstOrDefaultAsync(
                m => m.DriverId == driver.Id && m.IsPrimary,
                cancellationToken);
        }

        if (payoutMethod is null)
        {
            throw new BusinessRuleException("DRIVER_PAYOUT_METHOD_REQUIRED", "أضف طريقة سحب أساسية قبل طلب السحب | Add a primary payout method before requesting a withdrawal.");
        }

        EnsureSupportedBankPayoutMethod(payoutMethod.MethodType, payoutMethod.AccountIdentifier);

        var idempotencyKey = FirstNonEmpty(
            request.IdempotencyKey,
            HttpContext?.Request.Headers["Idempotency-Key"].FirstOrDefault());
        if (idempotencyKey?.Length > 160)
        {
            throw new BadRequestException(
                "WITHDRAWAL_IDEMPOTENCY_KEY_TOO_LONG",
                "Withdrawal idempotency key cannot exceed 160 characters.");
        }

        var dbContext = context as DbContext;

        DriverWithdrawalRequest? withdrawal;
        DriverWithdrawalRequestDto? existingResult;
        try
        {
            // The database connection is configured with EnableRetryOnFailure,
            // so any user-initiated transaction must be wrapped in the
            // provider execution strategy; otherwise EF throws
            // InvalidOperationException before the transaction runs. The whole
            // unit is idempotent and safe to retry as a block.
            if (dbContext is not null && dbContext.Database.IsRelational())
            {
                var strategy = dbContext.Database.CreateExecutionStrategy();
                (existingResult, withdrawal) = await strategy.ExecuteAsync(
                    () => CreateWithdrawalCoreAsync(
                        context,
                        dbContext,
                        driver,
                        payoutMethod,
                        request,
                        idempotencyKey,
                        limits,
                        useTransaction: true,
                        cancellationToken));
            }
            else
            {
                (existingResult, withdrawal) = await CreateWithdrawalCoreAsync(
                    context,
                    dbContext,
                    driver,
                    payoutMethod,
                    request,
                    idempotencyKey,
                    limits,
                    useTransaction: false,
                    cancellationToken);
            }
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (BadRequestException)
        {
            throw;
        }
        catch (Exception exception)
        {
            dbContext?.ChangeTracker.Clear();

            var sqlDiagnostic = DescribeSqlErrors(exception);
            logger.LogError(
                exception,
                "Failed to process driver withdrawal for driver {DriverId}. PaymentMethodId: {PaymentMethodId}. Amount: {Amount}. IdempotencyKey: {IdempotencyKey}. SqlErrors: {SqlErrors}",
                driver.Id,
                payoutMethod.Id,
                request.Amount,
                idempotencyKey,
                sqlDiagnostic);

            if (HttpContext is not null)
            {
                // Surfaced as a ProblemDetails extension so the true database
                // cause is visible to clients while diagnosing the withdrawal
                // save failure without needing server log access.
                HttpContext.Items["errorDiagnostic"] =
                    $"{exception.GetType().Name}: {exception.Message} | Sql: {sqlDiagnostic}";
            }

            throw MapWithdrawalDatabaseException(exception);
        }

        if (existingResult is not null)
        {
            return Ok(existingResult);
        }

        await DispatchWithdrawalSubmittedSideEffectsAsync(
            driver,
            withdrawal!,
            driverWalletNotificationService,
            adminAlertService,
            logger,
            cancellationToken);

        return Ok(MapWithdrawalDto(withdrawal!, payoutMethod));
    }

    /// <summary>
    /// Executes the transactional core of a withdrawal creation. Safe to run
    /// as a retriable unit inside a provider execution strategy: it clears the
    /// change tracker on entry, reloads the wallet, and either returns an
    /// existing (idempotent) result or the newly created withdrawal.
    /// </summary>
    private async Task<(DriverWithdrawalRequestDto? ExistingResult, DriverWithdrawalRequest? Created)> CreateWithdrawalCoreAsync(
        IApplicationDbContext context,
        DbContext? dbContext,
        Domain.Modules.Delivery.Entities.Driver driver,
        DriverPayoutMethod payoutMethod,
        CreateDriverWithdrawalRequest request,
        string? idempotencyKey,
        FinancialSettingsOptions limits,
        bool useTransaction,
        CancellationToken cancellationToken)
    {
        // On a retry the tracker may still hold entities added by the failed
        // attempt; start from a clean slate so nothing is double-inserted.
        dbContext?.ChangeTracker.Clear();

        var wallet = await GetOrCreateWalletAsync(context, driver.Id, cancellationToken);

        await using var transaction = useTransaction && dbContext is not null
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingByKey = await context.DriverWithdrawalRequests
                .Include(item => item.DriverPayoutMethod)
                .Include(item => item.Payout)
                .FirstOrDefaultAsync(
                    item => item.DriverId == driver.Id && item.RequestIdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (existingByKey is not null)
            {
                EnsureIdempotentWithdrawalMatches(existingByKey, request, payoutMethod.Id);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return (MapWithdrawalDto(existingByKey, existingByKey.DriverPayoutMethod), null);
            }
        }

        var activeWithdrawal = await context.DriverWithdrawalRequests
            .Include(item => item.DriverPayoutMethod)
            .Include(item => item.Payout)
            .Where(item =>
                item.DriverId == driver.Id &&
                (item.Status == DriverWithdrawalStatus.Pending ||
                 item.Status == DriverWithdrawalStatus.Processing))
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeWithdrawal is not null)
        {
            var isSameLegacyRetry = string.IsNullOrWhiteSpace(idempotencyKey) &&
                activeWithdrawal.Amount == request.Amount &&
                activeWithdrawal.DriverPayoutMethodId == payoutMethod.Id;
            if (isSameLegacyRetry)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return (MapWithdrawalDto(activeWithdrawal, activeWithdrawal.DriverPayoutMethod), null);
            }

            throw new BusinessRuleException(
                "DRIVER_ACTIVE_WITHDRAWAL_EXISTS",
                "The driver already has a pending or processing withdrawal request.");
        }

        var requestsToday = await context.DriverWithdrawalRequests.CountAsync(
            item => item.DriverId == driver.Id &&
                    item.CreatedAtUtc >= SaudiTime.StartOfTodayUtc &&
                    item.CreatedAtUtc < SaudiTime.StartOfTomorrowUtc,
            cancellationToken);
        if (limits.DriverMaximumWithdrawalRequestsPerDay > 0 &&
            requestsToday >= limits.DriverMaximumWithdrawalRequestsPerDay)
        {
            throw new BusinessRuleException(
                "DRIVER_DAILY_WITHDRAWAL_LIMIT_REACHED",
                "The daily withdrawal request limit has been reached.");
        }

        if (wallet.CodOwedBalance > 0)
        {
            throw new BusinessRuleException(
                "DRIVER_COD_DEBT_NOT_SETTLED",
                "لازم تسوي مبالغ الدفع عند الاستلام المستحقة قبل طلب السحب | Settle outstanding COD cash before requesting a withdrawal.");
        }

        var activeWithdrawalHolds = await SumActiveWithdrawalHoldsAsync(context, driver.Id, cancellationToken);
        var netWithdrawable = wallet.CurrentBalance - wallet.CodOwedBalance - wallet.PendingBalance - activeWithdrawalHolds;
        if (netWithdrawable < request.Amount)
        {
            throw new BusinessRuleException("INSUFFICIENT_WITHDRAWABLE_BALANCE", "مبلغ السحب يتجاوز الصافي المتاح بعد خصم الدفع عند الاستلام | Withdrawal amount exceeds net available balance after COD obligations.");
        }

        var withdrawal = new DriverWithdrawalRequest(
            driver.Id,
            wallet.Id,
            payoutMethod.Id,
            request.Amount,
            idempotencyKey,
            driver.PayoutDay);
        context.DriverWithdrawalRequests.Add(withdrawal);
        context.WalletHolds.Add(new WalletHold(
            WalletOwnerType.Driver,
            driver.Id,
            withdrawal.Amount,
            WalletHoldReason.Withdrawal,
            $"driver-withdrawal:{withdrawal.Id:N}",
            walletId: wallet.Id,
            referenceType: "DriverWithdrawalRequest",
            referenceId: withdrawal.Id,
            memo: "Driver withdrawal request submitted"));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (IsWithdrawalUniquenessConflict(exception))
        {
            await RollbackWithdrawalAttemptAsync(transaction, dbContext, cancellationToken);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existingByKey = await context.DriverWithdrawalRequests
                    .AsNoTracking()
                    .Include(item => item.DriverPayoutMethod)
                    .Include(item => item.Payout)
                    .FirstOrDefaultAsync(
                        item => item.DriverId == driver.Id && item.RequestIdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (existingByKey is not null)
                {
                    EnsureIdempotentWithdrawalMatches(existingByKey, request, payoutMethod.Id);
                    return (MapWithdrawalDto(existingByKey, existingByKey.DriverPayoutMethod), null);
                }
            }

            throw new BusinessRuleException(
                "DRIVER_ACTIVE_WITHDRAWAL_EXISTS",
                "The driver already has a pending or processing withdrawal request.");
        }

        return (null, withdrawal);
    }

    [HttpGet("withdrawals")]
    public async Task<ActionResult<DriverWithdrawalRequestListDto>> GetWithdrawals(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = IncludeWithdrawalGraph(context.DriverWithdrawalRequests.AsNoTracking())
            .Where(w => w.DriverId == driver.Id)
            .OrderByDescending(w => w.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new DriverWithdrawalRequestListDto(
            items.Select(item => MapWithdrawalDto(item, item.DriverPayoutMethod)).ToList(),
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("withdrawals/{id:guid}/transfer-proof")]
    public async Task<IActionResult> DownloadWithdrawalTransferProof(
        Guid id,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] PayoutProofAttachmentService payoutProofAttachmentService,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);

        var withdrawal = await context.DriverWithdrawalRequests
            .AsNoTracking()
            .Include(item => item.Payout)!
                .ThenInclude(payout => payout!.ManualConfirmation)
            .FirstOrDefaultAsync(
                item => item.Id == id && item.DriverId == driver.Id,
                cancellationToken)
            ?? throw new NotFoundException("DriverWithdrawalRequest", id);

        if (withdrawal.Status != DriverWithdrawalStatus.Paid)
        {
            throw new NotFoundException("WithdrawalTransferProof", id);
        }

        var payout = withdrawal.Payout;
        var proofAttachmentId = payout?.ManualConfirmation?.ProofAttachmentId;
        if (payout is null ||
            payout.Status != PayoutStatus.Paid ||
            !proofAttachmentId.HasValue ||
            proofAttachmentId.Value == Guid.Empty)
        {
            throw new NotFoundException("WithdrawalTransferProof", id);
        }

        var proof = await payoutProofAttachmentService.GetForDownloadAsync(
            payout.Id,
            proofAttachmentId.Value,
            cancellationToken);

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File(proof.Content, proof.ContentType, proof.FileName);
    }

    [HttpPost("withdrawals/{id:guid}/cancel")]
    [EnableRateLimiting(RateLimitPolicyNames.WalletMutations)]
    public async Task<ActionResult<DriverWithdrawalRequestDto>> CancelWithdrawal(
        Guid id,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDriverWalletNotificationService driverWalletNotificationService,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var withdrawal = await context.DriverWithdrawalRequests
            .Include(item => item.DriverPayoutMethod)
            .Include(item => item.Payout)
            .FirstOrDefaultAsync(
                item => item.Id == id && item.DriverId == driver.Id,
                cancellationToken)
            ?? throw new NotFoundException("DriverWithdrawalRequest", id);

        if (withdrawal.Status == DriverWithdrawalStatus.Cancelled)
        {
            return Ok(MapWithdrawalDto(withdrawal, withdrawal.DriverPayoutMethod));
        }

        if (withdrawal.Status != DriverWithdrawalStatus.Pending || withdrawal.PayoutId.HasValue)
        {
            throw new BusinessRuleException(
                "DRIVER_WITHDRAWAL_CANNOT_CANCEL",
                "Only a pending withdrawal that has not entered finance processing can be cancelled by the driver.");
        }

        var holds = await context.WalletHolds
            .Where(item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == driver.Id &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.Status == WalletHoldStatus.Active &&
                item.ReferenceType == "DriverWithdrawalRequest" &&
                item.ReferenceId == withdrawal.Id)
            .ToListAsync(cancellationToken);
        withdrawal.Cancel("Cancelled by driver.");
        foreach (var hold in holds)
        {
            hold.Cancel("Cancelled by driver.");
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (context is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }

            var latest = await context.DriverWithdrawalRequests
                .AsNoTracking()
                .Include(item => item.DriverPayoutMethod)
                .Include(item => item.Payout)
                .FirstOrDefaultAsync(
                    item => item.Id == id && item.DriverId == driver.Id,
                    cancellationToken)
                ?? throw new NotFoundException("DriverWithdrawalRequest", id);
            if (latest.Status == DriverWithdrawalStatus.Cancelled)
            {
                return Ok(MapWithdrawalDto(latest, latest.DriverPayoutMethod));
            }

            throw new BusinessRuleException(
                "DRIVER_WITHDRAWAL_CANNOT_CANCEL",
                "The withdrawal entered finance processing before cancellation completed. Refresh the wallet and review its current status.");
        }

        await driverWalletNotificationService.NotifyWithdrawalCancelledAsync(
            driver.UserId,
            withdrawal,
            cancellationToken);
        return Ok(MapWithdrawalDto(withdrawal, withdrawal.DriverPayoutMethod));
    }

    private static bool IsWithdrawalUniquenessConflict(DbUpdateException exception)
    {
        var details = DescribeExceptionChain(exception);
        if (details.Contains("UX_DriverWithdrawalRequests_OneActivePerDriver", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("UX_DriverWithdrawalRequests_Driver_IdempotencyKey", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("IX_WalletHolds_IdempotencyKey", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContainsSqlErrorNumber(exception, 2601, 2627) &&
               (details.Contains("DriverWithdrawalRequests", StringComparison.OrdinalIgnoreCase) ||
                details.Contains("WalletHolds", StringComparison.OrdinalIgnoreCase));
    }

    private static Exception MapWithdrawalDatabaseException(Exception exception)
    {
        var details = DescribeExceptionChain(exception);
        if (details.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            return new BusinessRuleException(
                "WITHDRAWAL_WORKFLOW_NOT_READY",
                "نظام السحب ما زال يُحدَّث على الخادم. حاول مرة أخرى بعد دقائق أو تواصل مع الدعم. | The withdrawal workflow is still being updated on the server. Try again in a few minutes or contact support.");
        }

        if (details.Contains("String or binary data would be truncated", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("max length", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("MaxLength", StringComparison.OrdinalIgnoreCase) ||
            ContainsSqlErrorNumber(exception, 8152, 2628))
        {
            return new BusinessRuleException(
                "WITHDRAWAL_DESTINATION_SNAPSHOT_TOO_LARGE",
                "تعذر حفظ بيانات حساب السحب لأنها طويلة جدًا. حدّث طريقة السحب أو تواصل مع الدعم. | The payout destination snapshot is too large to save. Update the payout method or contact support.");
        }

        if (ContainsSqlErrorNumber(exception, 1205, 1222))
        {
            return new BusinessRuleException(
                "WITHDRAWAL_TEMPORARY_DATABASE_CONFLICT",
                "الخادم مشغول بمعالجة طلب آخر. حاول مرة أخرى بعد ثوانٍ. | The server is busy processing another request. Please retry in a few seconds.");
        }

        if (details.Contains("IX_WalletHolds_IdempotencyKey", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("UX_DriverWithdrawalRequests_OneActivePerDriver", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("UX_DriverWithdrawalRequests_Driver_IdempotencyKey", StringComparison.OrdinalIgnoreCase) ||
            (ContainsSqlErrorNumber(exception, 2601, 2627) &&
             (details.Contains("DriverWithdrawalRequests", StringComparison.OrdinalIgnoreCase) ||
              details.Contains("WalletHolds", StringComparison.OrdinalIgnoreCase))))
        {
            return new BusinessRuleException(
                "DRIVER_ACTIVE_WITHDRAWAL_EXISTS",
                "The driver already has a pending or processing withdrawal request.");
        }

        if (ContainsSqlErrorNumber(exception, 2601, 2627) &&
            details.Contains("IX_Wallet_Owner", StringComparison.OrdinalIgnoreCase))
        {
            return new BusinessRuleException(
                "WITHDRAWAL_WALLET_INITIALIZING",
                "جاري تجهيز محفظتك. حاول طلب السحب مرة أخرى بعد ثوانٍ. | Your wallet is still being initialized. Please retry the withdrawal in a few seconds.");
        }

        return new BusinessRuleException(
            "WITHDRAWAL_SAVE_FAILED",
            "تعذر حفظ طلب السحب الآن. حاول مرة أخرى بعد قليل. | The withdrawal request could not be saved right now. Please try again shortly.");
    }

    private static bool ContainsSqlErrorNumber(Exception exception, params int[] errorNumbers)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    if (errorNumbers.Contains(error.Number))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string DescribeExceptionChain(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts);
    }

    private static string DescribeSqlErrors(Exception exception)
    {
        var errors = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
            {
                continue;
            }

            foreach (SqlError error in sqlException.Errors)
            {
                errors.Add($"{error.Number}:{error.Message}");
            }
        }

        return errors.Count == 0 ? "none" : string.Join(" | ", errors);
    }

    private static async Task RollbackWithdrawalAttemptAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        DbContext? dbContext,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        dbContext?.ChangeTracker.Clear();
    }

    private static async Task DispatchWithdrawalSubmittedSideEffectsAsync(
        Domain.Modules.Delivery.Entities.Driver driver,
        DriverWithdrawalRequest withdrawal,
        IDriverWalletNotificationService driverWalletNotificationService,
        IAdminAlertService adminAlertService,
        ILogger<DriverWalletController> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await driverWalletNotificationService.NotifyWithdrawalSubmittedAsync(
                driver.UserId,
                withdrawal,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Driver withdrawal {WithdrawalId} was saved, but driver notification dispatch failed.",
                withdrawal.Id);
        }

        try
        {
            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.SettlementRequested,
                    AdminAlertCategories.Settlements,
                    AdminAlertPriorities.High,
                    "طلب سحب للمندوب يحتاج إلى مراجعة",
                    "Driver withdrawal requires review",
                    $"قدّم المندوب {GetDriverDisplayName(driver)} طلب سحب بقيمة {withdrawal.Amount:0.##} ر.س.",
                    $"Driver {GetDriverDisplayName(driver)} requested withdrawal of {withdrawal.Amount:0.##}.",
                    withdrawal.Id,
                    $"/finances/withdrawals?focus={withdrawal.Id:D}",
                    new
                    {
                        withdrawalId = withdrawal.Id,
                        driverId = driver.Id,
                        driverUserId = driver.UserId,
                        amount = withdrawal.Amount,
                        status = withdrawal.Status.ToString()
                    }),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Driver withdrawal {WithdrawalId} was saved, but admin alert dispatch failed.",
                withdrawal.Id);
        }
    }

    private static void EnsureIdempotentWithdrawalMatches(
        DriverWithdrawalRequest existing,
        CreateDriverWithdrawalRequest request,
        Guid payoutMethodId)
    {
        if (existing.Amount == request.Amount && existing.DriverPayoutMethodId == payoutMethodId)
        {
            return;
        }

        throw new BusinessRuleException(
            "WITHDRAWAL_IDEMPOTENCY_KEY_REUSED",
            "This idempotency key was already used for a different withdrawal request.");
    }

    private static async Task<Domain.Modules.Delivery.Entities.Driver> GetDriverAsync(
        ICurrentUserService currentUserService,
        IDriverRepository driverRepository,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        if (driver.User?.IsLoginLocked == true || !driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_WALLET_ACCESS_BLOCKED",
                "Wallet access is available after the driver account is active and approved.");
        }

        return driver;
    }

    private static async Task<DriverPayoutPreferenceDto> ToPayoutPreferenceDtoAsync(
        Domain.Modules.Delivery.Entities.Driver driver,
        ISettlementProcessingSettingsService settlementProcessingSettingsService,
        CancellationToken cancellationToken)
    {
        var enabledDays = await settlementProcessingSettingsService
            .GetEnabledPayoutDaysAsync(cancellationToken);
        return new DriverPayoutPreferenceDto(
            driver.PayoutDay.ToString(),
            enabledDays.Select(day => day.ToString()).ToArray());
    }

    private static async Task<Wallet> GetOrCreateWalletAsync(
        IApplicationDbContext context,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var wallet = await context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == WalletOwnerType.Driver && w.OwnerId == driverId, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet(WalletOwnerType.Driver, driverId);
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private static async Task<decimal> SumIncomingAsync(
        IApplicationDbContext context,
        Guid walletId,
        DateTime fromUtc,
        CancellationToken cancellationToken)
    {
        return await context.WalletTransactions
            .Where(t => t.WalletId == walletId && t.Direction == "IN" && t.CreatedAtUtc >= fromUtc)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
    }

    private static async Task<DriverWithdrawalSummaryDto> BuildWithdrawalSummaryAsync(
        IApplicationDbContext context,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var query = context.DriverWithdrawalRequests
            .AsNoTracking()
            .Where(w => w.DriverId == driverId);

        var pendingQuery = query.Where(w =>
            w.Status == DriverWithdrawalStatus.Pending ||
            w.Status == DriverWithdrawalStatus.Processing);

        var pendingCount = await pendingQuery.CountAsync(cancellationToken);
        var pendingAmount = await pendingQuery.SumAsync(w => (decimal?)w.Amount, cancellationToken) ?? 0m;
        var totalRequests = await query.CountAsync(cancellationToken);

        return new DriverWithdrawalSummaryDto(pendingCount, pendingAmount, totalRequests);
    }

    private static DriverPayoutMethodType ParseMethodType(string value)
    {
        if (!Enum.TryParse<DriverPayoutMethodType>(value, true, out var methodType))
        {
            throw new BusinessRuleException("INVALID_DRIVER_PAYOUT_METHOD_TYPE", "نوع طريقة السحب غير مدعوم | Unsupported payout method type.");
        }

        return methodType;
    }

    private static void EnsureSupportedBankPayoutMethod(DriverPayoutMethodType methodType, string accountIdentifier)
    {
        if (methodType != DriverPayoutMethodType.BankAccount)
        {
            throw new BusinessRuleException(
                "DRIVER_BANK_ACCOUNT_REQUIRED",
                "Only bank account payout methods are supported for withdrawals.");
        }

        if (!IsValidSaudiIban(accountIdentifier))
        {
            throw new BusinessRuleException(
                "DRIVER_BANK_IBAN_INVALID",
                "Driver bank account must be a valid Saudi IBAN.");
        }
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

    private static void EnsurePayoutMethodRequest(CreateDriverPayoutMethodRequest? request)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        EnsureRequiredString(request.Type, "INVALID_DRIVER_PAYOUT_METHOD_TYPE", "Payout method type is required.");
        EnsureRequiredString(request.AccountHolderName, "INVALID_ACCOUNT_HOLDER_NAME", "Account holder name is required.");
        EnsureRequiredString(request.AccountIdentifier, "INVALID_ACCOUNT_IDENTIFIER", "Account identifier is required.");
    }

    private static void EnsurePayoutMethodRequest(UpdateDriverPayoutMethodRequest? request)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        EnsureRequiredString(request.Type, "INVALID_DRIVER_PAYOUT_METHOD_TYPE", "Payout method type is required.");
        EnsureRequiredString(request.AccountHolderName, "INVALID_ACCOUNT_HOLDER_NAME", "Account holder name is required.");
        EnsureRequiredString(request.AccountIdentifier, "INVALID_ACCOUNT_IDENTIFIER", "Account identifier is required.");
    }

    private static ProfileChangeApprovalAlert BuildDriverPayoutMethodApprovalAlert(
        Domain.Modules.Delivery.Entities.Driver driver,
        string action,
        Guid? payoutMethodId = null)
    {
        var driverName = GetDriverDisplayName(driver);
        return new(
            AdminAlertTypes.DriverCriticalChangeSubmitted,
            AdminAlertCategories.Drivers,
            AdminAlertPriorities.High,
            "تغيير طريقة سحب مندوب بانتظار الاعتماد",
            "Driver payout method change pending approval",
            $"أرسل المندوب {driverName} تغييرًا في طريقة السحب وينتظر اعتماد المشرف.",
            $"Driver {driverName} submitted payout method changes pending admin approval.",
            payoutMethodId ?? driver.Id,
            "/admin/access/approvals",
            new
            {
                driverId = driver.Id,
                userId = driver.UserId,
                payoutMethodId,
                section = "payout_method",
                action
            });
    }

    private static string GetDriverDisplayName(Domain.Modules.Delivery.Entities.Driver driver) =>
        string.IsNullOrWhiteSpace(driver.User?.FullName)
            ? driver.UserId.ToString("N")
            : driver.User.FullName.Trim();

    private static void EnsureRequiredString(string? value, string errorCode, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException(errorCode, message);
        }
    }

    private static Expression<Func<WalletTransaction, DriverWalletTransactionDto>> MapTransaction() =>
        item => new DriverWalletTransactionDto(
            item.Id,
            item.TxnType.ToString(),
            item.Direction,
            item.Amount,
            item.Description,
            item.ReferenceType,
            item.ReferenceId.HasValue ? item.ReferenceId.Value.ToString() : null,
            item.CreatedAtUtc);

    private static Expression<Func<DriverPayoutMethod, DriverPayoutMethodDto>> MapPayoutMethod() =>
        item => new DriverPayoutMethodDto(
            item.Id,
            item.MethodType.ToString(),
            item.AccountHolderName,
            item.ProviderName,
            item.MaskedLabel,
            item.IsPrimary,
            item.IsVerified);

    private static DriverPayoutMethodDto MapPayoutMethodDto(DriverPayoutMethod item) =>
        new(
            item.Id,
            item.MethodType.ToString(),
            item.AccountHolderName,
            item.ProviderName,
            item.MaskedLabel,
            item.IsPrimary,
            item.IsVerified);

    private static IQueryable<DriverWithdrawalRequest> IncludeWithdrawalGraph(
        IQueryable<DriverWithdrawalRequest> query) =>
        query
            .Include(item => item.DriverPayoutMethod)
            .Include(item => item.Payout)!
                .ThenInclude(payout => payout!.ManualConfirmation)!
                    .ThenInclude(confirmation => confirmation!.ProofAttachment);

    private static DriverWithdrawalRequestDto MapWithdrawalDto(
        DriverWithdrawalRequest withdrawal,
        DriverPayoutMethod payoutMethod)
    {
        var exposeTransferDetails = withdrawal.Status == DriverWithdrawalStatus.Paid;
        var proofAttachmentId = withdrawal.Payout?.ManualConfirmation?.ProofAttachmentId;
        var hasTransferProof = exposeTransferDetails &&
            proofAttachmentId.HasValue &&
            proofAttachmentId.Value != Guid.Empty;
        var transferProofFileName = hasTransferProof
            ? withdrawal.Payout?.ManualConfirmation?.ProofAttachment?.FileName
            : null;

        return new(
            withdrawal.Id,
            withdrawal.Amount,
            withdrawal.Status.ToString(),
            exposeTransferDetails ? withdrawal.TransferReference : null,
            withdrawal.FailureReason,
            withdrawal.CreatedAtUtc,
            withdrawal.ProcessedAtUtc,
            MapPayoutMethodDto(payoutMethod),
            withdrawal.PayoutId,
            withdrawal.Payout?.ProviderName,
            exposeTransferDetails ? withdrawal.Payout?.ProviderTransferId : null,
            withdrawal.RequestedPayoutDay?.ToString(),
            hasTransferProof,
            transferProofFileName);
    }

    private static async Task<decimal> SumActiveWithdrawalHoldsAsync(
        IApplicationDbContext context,
        Guid driverId,
        CancellationToken cancellationToken) =>
        await context.WalletHolds
            .AsNoTracking()
            .Where(item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == driverId &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.Status == WalletHoldStatus.Active)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
