using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Application.Modules.Wallets.Interfaces;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

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
    public async Task<ActionResult<DriverWithdrawalRequestDto>> CreateWithdrawal(
        [FromBody] CreateDriverWithdrawalRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] INotificationService notificationService,
        [FromServices] IOneSignalPushService oneSignalPushService,
        [FromServices] IAdminAlertService adminAlertService,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        if (request.Amount <= 0)
        {
            throw new BadRequestException(
                "INVALID_WITHDRAWAL_AMOUNT",
                "Withdrawal amount must be greater than zero.");
        }

        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(context, driver.Id, cancellationToken);

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

        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, request.Amount);
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
        await context.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "wallet",
            @event: "wallet.withdrawal_submitted",
            withdrawalId: withdrawal.Id,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString()
            });

        await notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                "استلمنا طلب السحب",
                "Withdrawal request submitted",
                $"استلمنا طلب سحب بقيمة {withdrawal.Amount:0.##}.",
                $"Your withdrawal request for {withdrawal.Amount:0.##} was submitted.",
                NotificationTypes.DriverWalletUpdated,
                NotificationCategories.Wallet,
                NotificationPriorities.Normal,
                withdrawal.Id,
                data),
            cancellationToken);

        await notificationService.SendDriverWalletUpdatedAsync(driver.UserId, cancellationToken);

        await oneSignalPushService.SendMobileNotificationAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driver.UserId.ToString(),
                "\u062a\u0645 \u0627\u0633\u062a\u0644\u0627\u0645 \u0637\u0644\u0628 \u0627\u0644\u0633\u062d\u0628",
                "Withdrawal request submitted",
                $"\u062a\u0645 \u0627\u0633\u062a\u0644\u0627\u0645 \u0637\u0644\u0628 \u0633\u062d\u0628 \u0628\u0642\u064a\u0645\u0629 {withdrawal.Amount:0.##}.",
                $"Your withdrawal request for {withdrawal.Amount:0.##} was submitted.",
                NotificationTypes.DriverWalletUpdated,
                withdrawal.Id,
                data,
                "/wallet",
                NotificationCategories.Wallet,
                OneSignalApplicationTarget.Driver),
            cancellationToken);

        await adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementRequested,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.High,
                "Driver withdrawal requires review",
                "Driver withdrawal requires review",
                $"Driver {driver.User.FullName} requested withdrawal of {withdrawal.Amount:0.##}.",
                $"Driver {driver.User.FullName} requested withdrawal of {withdrawal.Amount:0.##}.",
                withdrawal.Id,
                "/finances/withdrawals",
                new
                {
                    withdrawalId = withdrawal.Id,
                    driverId = driver.Id,
                    driverUserId = driver.UserId,
                    amount = withdrawal.Amount,
                    status = withdrawal.Status.ToString()
                }),
            cancellationToken);

        return Ok(MapWithdrawalDto(withdrawal, payoutMethod));
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

        var query = context.DriverWithdrawalRequests
            .AsNoTracking()
            .Include(w => w.DriverPayoutMethod)
            .Include(w => w.Payout)
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

    private static DriverWithdrawalRequestDto MapWithdrawalDto(
        DriverWithdrawalRequest withdrawal,
        DriverPayoutMethod payoutMethod) =>
        new(
            withdrawal.Id,
            withdrawal.Amount,
            withdrawal.Status.ToString(),
            withdrawal.TransferReference,
            withdrawal.FailureReason,
            withdrawal.CreatedAtUtc,
            withdrawal.ProcessedAtUtc,
            MapPayoutMethodDto(payoutMethod),
            withdrawal.PayoutId,
            withdrawal.Payout?.ProviderName,
            withdrawal.Payout?.ProviderTransferId);

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
}
