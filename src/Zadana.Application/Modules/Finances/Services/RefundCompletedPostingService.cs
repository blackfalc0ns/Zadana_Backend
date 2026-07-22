using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Settings;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Posts <see cref="FinancialEventType.RefundCompleted"/> when a support-case refund succeeds.
/// </summary>
public sealed class RefundCompletedPostingService
{
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly FinancialSettingsOptions _settings;
    private readonly ILogger<RefundCompletedPostingService> _logger;

    public RefundCompletedPostingService(
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater,
        ILogger<RefundCompletedPostingService> logger,
        IOptions<FinancialSettingsOptions>? settings = null)
    {
        _postingService = postingService;
        _walletProjectionUpdater = walletProjectionUpdater;
        _logger = logger;
        _settings = settings?.Value ?? new FinancialSettingsOptions();
    }

    public static string BuildIdempotencyKey(Guid refundId) => $"refund-completed:{refundId:N}";

    public async Task PostAsync(
        Refund refund,
        Order order,
        Payment payment,
        RefundAllocation allocation,
        Guid? driverId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(allocation);

        if (refund.LifecycleStatus != RefundStatus.Succeeded)
        {
            return;
        }

        var refundAmount = refund.ApprovedAmount;
        if (refundAmount <= 0m)
        {
            return;
        }

        allocation.EnsureBalances(refundAmount);

        if (!CurrencyPolicy.IsOfficial(refund.Currency))
        {
            _logger.LogWarning(
                "[RefundCompletedPosting] Skipping refund {RefundId} because currency {Currency} is not SAR.",
                refund.Id,
                refund.Currency);
            return;
        }

        var lines = BuildLines(
            order,
            payment,
            allocation,
            driverId,
            refundAmount);

        if (lines.Count == 0)
        {
            _logger.LogWarning(
                "[RefundCompletedPosting] Refund {RefundId} produced no journal lines.",
                refund.Id);
            return;
        }

        var posting = await _postingService.PostAsync(
            FinancialEventType.RefundCompleted,
            BuildIdempotencyKey(refund.Id),
            lines,
            orderId: order.Id,
            refundId: refund.Id,
            currencyCode: CurrencyPolicy.OfficialCurrency,
            description: $"Refund completed for order {order.OrderNumber}",
            cancellationToken: cancellationToken);

        if (!posting.WasAlreadyPosted)
        {
            await _walletProjectionUpdater.ApplyJournalEntryAsync(posting.JournalEntryId, cancellationToken);
            _logger.LogInformation(
                "[RefundCompletedPosting] Posted refund {RefundId} for order {OrderId}.",
                refund.Id,
                order.Id);
        }
    }

    private List<JournalLineDraft> BuildLines(
        Order order,
        Payment payment,
        RefundAllocation allocation,
        Guid? driverId,
        decimal refundAmount)
    {
        var lines = new List<JournalLineDraft>();

        if (allocation.PlatformAbsorbedAmount > 0m)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.RefundExpense,
                allocation.PlatformAbsorbedAmount,
                0m,
                FinancialOwnerType.Platform,
                _settings.PlatformWalletOwnerId,
                order.Id,
                Memo: $"Platform refund expense for order {order.OrderNumber}"));
        }

        if (allocation.VendorRecoveryAmount > 0m)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.VendorPayable,
                allocation.VendorRecoveryAmount,
                0m,
                FinancialOwnerType.Vendor,
                order.VendorId,
                order.Id,
                Memo: $"Vendor refund recovery for order {order.OrderNumber}"));
        }

        if (allocation.DriverRecoveryAmount > 0m)
        {
            if (driverId is null)
            {
                lines.Add(new JournalLineDraft(
                    FinancialAccountCode.RefundExpense,
                    allocation.DriverRecoveryAmount,
                    0m,
                    FinancialOwnerType.Platform,
                    _settings.PlatformWalletOwnerId,
                    order.Id,
                    Memo: $"Driver refund recovery reclassified to platform for order {order.OrderNumber}"));
            }
            else
            {
                lines.Add(new JournalLineDraft(
                    FinancialAccountCode.DriverPayable,
                    allocation.DriverRecoveryAmount,
                    0m,
                    FinancialOwnerType.Driver,
                    driverId,
                    order.Id,
                    Memo: $"Driver refund recovery for order {order.OrderNumber}"));
            }
        }

        var (creditAccount, creditOwnerType, creditOwnerId) = ResolveRefundFundingAccount(payment.Method);
        lines.Add(new JournalLineDraft(
            creditAccount,
            0m,
            refundAmount,
            creditOwnerType,
            creditOwnerType == FinancialOwnerType.Platform ? _settings.PlatformWalletOwnerId : creditOwnerId,
            order.Id,
            Memo: $"Refund funding for order {order.OrderNumber}"));

        return lines;
    }

    private static (FinancialAccountCode Account, FinancialOwnerType? OwnerType, Guid? OwnerId) ResolveRefundFundingAccount(
        PaymentMethodType paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethodType.Card or PaymentMethodType.Mada or PaymentMethodType.ApplePay =>
                (FinancialAccountCode.GatewayReceivable, FinancialOwnerType.Gateway, null),
            _ => (FinancialAccountCode.PlatformCash, FinancialOwnerType.Platform, null)
        };
}
