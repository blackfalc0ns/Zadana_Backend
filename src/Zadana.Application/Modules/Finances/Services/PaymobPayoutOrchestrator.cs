using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class PaymobPayoutOrchestrator
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobPayoutGateway _paymobPayoutGateway;
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly FinancialSettingsOptions _settings;
    private readonly IAdminAlertService _adminAlertService;

    public PaymobPayoutOrchestrator(
        IApplicationDbContext context,
        IPaymobPayoutGateway paymobPayoutGateway,
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater,
        IOptions<FinancialSettingsOptions> settings,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _paymobPayoutGateway = paymobPayoutGateway;
        _postingService = postingService;
        _walletProjectionUpdater = walletProjectionUpdater;
        _settings = settings.Value;
        _adminAlertService = adminAlertService;
    }

    public async Task<Payout> TriggerAsync(Guid payoutId, Guid? processedByUserId = null, bool isRetry = false, CancellationToken cancellationToken = default)
    {
        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);

        if (!_paymobPayoutGateway.IsEnabled)
        {
            throw new BusinessRuleException("PAYMOB_PAYOUT_DISABLED", "Paymob payouts are not enabled.");
        }

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Cancelled)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "Closed payouts cannot be triggered.");
        }

        payout.MarkAsProcessing();
        payout.Settlement.MarkAsProcessing();
        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
            PayoutStatus.Processing));

        PaymobPayoutResult result;
        try
        {
            result = await _paymobPayoutGateway.TriggerPayoutAsync(
                new PaymobPayoutRequest(
                    payout.Id,
                    payout.Amount,
                    "EGP",
                    payout.DestinationType.ToString(),
                    payout.DestinationSnapshot ?? string.Empty,
                    payout.TransferReference ?? payout.Id.ToString("N")),
                cancellationToken);
        }
        catch (Exception ex)
        {
            await SendPayoutIntegrationFailureAlertAsync(payout, ex, cancellationToken);
            throw;
        }

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
            result.IsAccepted ? PayoutStatus.Queued : PayoutStatus.Failed,
            providerTransferId: result.ProviderTransferId,
            transferReference: result.TransferReference,
            failureReason: result.FailureReason,
            rawPayload: result.RawPayload));

        if (result.IsAccepted)
        {
            payout.MarkQueued(result.ProviderTransferId);
        }
        else
        {
            payout.MarkAsFailed(result.FailureReason);
            payout.Settlement.MarkPayoutFailed();
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (!result.IsAccepted)
        {
            await SendPayoutFailedAlertAsync(payout, result.FailureReason, cancellationToken);
        }

        return payout;
    }

    public async Task<Payout> ApplyProviderCallbackAsync(PaymobPayoutWebhookNotification notification, CancellationToken cancellationToken = default)
    {
        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .FirstOrDefaultAsync(item => item.ProviderTransferId == notification.ProviderTransferId, cancellationToken)
            ?? throw new NotFoundException("Payout provider transfer", Guid.Empty);

        var normalizedStatus = notification.Status.Trim().ToLowerInvariant();
        var attemptStatus = normalizedStatus is "paid" or "success" or "succeeded"
            ? PayoutStatus.Paid
            : normalizedStatus is "failed" or "failure" or "rejected"
                ? PayoutStatus.Failed
                : PayoutStatus.Processing;

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ProviderCallback,
            attemptStatus,
            providerTransferId: notification.ProviderTransferId,
            transferReference: notification.TransferReference,
            failureReason: notification.FailureReason,
            rawPayload: notification.RawPayload));

        if (attemptStatus == PayoutStatus.Paid && payout.Status != PayoutStatus.Paid)
        {
            payout.MarkAsPaid(notification.TransferReference ?? notification.ProviderTransferId);
            payout.Settlement.MarkPaidOut();
            await PostPayoutPaidAsync(payout, cancellationToken);
        }
        else if (attemptStatus == PayoutStatus.Failed)
        {
            payout.MarkAsFailed(notification.FailureReason);
            payout.Settlement.MarkPayoutFailed();
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (attemptStatus == PayoutStatus.Failed)
        {
            await SendPayoutFailedAlertAsync(payout, notification.FailureReason, cancellationToken);
        }

        return payout;
    }

    public async Task CancelAsync(Guid payoutId, CancellationToken cancellationToken = default)
    {
        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);

        if (payout.Status == PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_PAID", "Paid payouts cannot be cancelled.");
        }

        payout.Cancel();
        payout.Settlement.Hold();
        _context.PayoutAttempts.Add(new PayoutAttempt(payout.Id, PayoutAttemptType.Cancel, PayoutStatus.Cancelled));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task PostPayoutPaidAsync(Payout payout, CancellationToken cancellationToken)
    {
        var settlement = payout.Settlement;
        var payableAccount = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialAccountCode.DriverPayable
            : FinancialAccountCode.VendorPayable;
        var ownerType = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialOwnerType.Driver
            : FinancialOwnerType.Vendor;
        var eventType = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialEventType.DriverPayoutPaid
            : FinancialEventType.VendorPayoutPaid;

        var result = await _postingService.PostAsync(
            eventType,
            $"payout-paid:{payout.Id:N}:{payout.ProviderTransferId ?? payout.TransferReference}",
            [
                new JournalLineDraft(
                    payableAccount,
                    payout.Amount,
                    0m,
                    ownerType,
                    settlement.OwnerId,
                    SettlementId: settlement.Id,
                    PayoutId: payout.Id,
                    Memo: $"Payout paid {payout.Id}"),
                new JournalLineDraft(
                    FinancialAccountCode.PlatformCash,
                    0m,
                    payout.Amount,
                    FinancialOwnerType.Platform,
                    _settings.PlatformWalletOwnerId,
                    SettlementId: settlement.Id,
                    PayoutId: payout.Id,
                    Memo: $"Platform cash payout {payout.Id}")
            ],
            settlementId: settlement.Id,
            payoutId: payout.Id,
            description: $"Payout paid {payout.Id}",
            cancellationToken: cancellationToken);

        await _walletProjectionUpdater.ApplyJournalEntryAsync(result.JournalEntryId, cancellationToken);
    }

    private Task SendPayoutFailedAlertAsync(Payout payout, string? failureReason, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(failureReason) ? "Provider did not return a failure reason." : failureReason.Trim();

        return _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementFailed,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.Critical,
                "فشل تحويل تسوية",
                "Settlement payout failed",
                $"فشل تحويل تسوية بقيمة {payout.Amount:N2}. السبب: {reason}",
                $"A settlement payout for {payout.Amount:N2} failed. Reason: {reason}",
                payout.Id,
                "/finances/settlements",
                new
                {
                    payoutId = payout.Id,
                    settlementId = payout.SettlementId,
                    amount = payout.Amount,
                    providerTransferId = payout.ProviderTransferId,
                    transferReference = payout.TransferReference,
                    failureReason = reason
                }),
            cancellationToken);
    }

    private Task SendPayoutIntegrationFailureAlertAsync(Payout payout, Exception exception, CancellationToken cancellationToken)
    {
        return _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SystemIntegrationFailure,
                AdminAlertCategories.System,
                AdminAlertPriorities.Critical,
                "فشل تكامل Paymob Payout",
                "Paymob payout integration failure",
                $"حدث خطأ أثناء إرسال تحويل Paymob للتسوية {payout.SettlementId}.",
                $"Paymob payout trigger failed for settlement {payout.SettlementId}.",
                payout.Id,
                "/finances/settlements",
                new
                {
                    payoutId = payout.Id,
                    settlementId = payout.SettlementId,
                    amount = payout.Amount,
                    exceptionType = exception.GetType().Name,
                    message = exception.Message
                }),
            cancellationToken);
    }
}
