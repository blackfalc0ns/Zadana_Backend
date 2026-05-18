using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Provider-agnostic payout orchestrator. When an <see cref="IPayoutGateway"/>
/// is registered and enabled it forwards the payout to the gateway; otherwise
/// it records the trigger as a manual operation that admins can complete by
/// calling <see cref="MarkPaidAsync"/>.
/// </summary>
public sealed class PayoutOrchestrator
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IPayoutGateway> _payoutGateways;
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly FinancialSettingsOptions _settings;
    private readonly IAdminAlertService _adminAlertService;

    public PayoutOrchestrator(
        IApplicationDbContext context,
        IEnumerable<IPayoutGateway> payoutGateways,
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater,
        IOptions<FinancialSettingsOptions> settings,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _payoutGateways = payoutGateways;
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

        var gateway = _payoutGateways.FirstOrDefault(g => g.IsEnabled);

        if (gateway is null)
        {
            // Manual mode - admin will mark it paid using MarkPaidAsync once the bank transfer settles.
            payout.MarkQueued();
            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
                PayoutStatus.Queued,
                providerName: "Manual",
                providerTransferId: null,
                transferReference: payout.TransferReference,
                failureReason: null,
                rawPayload: null));
            await _context.SaveChangesAsync(cancellationToken);
            return payout;
        }

        try
        {
            var result = await gateway.CreatePayoutAsync(
                new CreatePayoutCommand(
                    PayoutId: payout.Id,
                    OwnerId: payout.Settlement.OwnerId,
                    OwnerType: payout.Settlement.OwnerType.ToString(),
                    Amount: payout.Amount,
                    Currency: "SAR",
                    IdempotencyKey: $"payout:{payout.Id:N}",
                    BeneficiaryName: null,
                    BeneficiaryIban: null,
                    BeneficiaryBankCode: null,
                    Reference: payout.TransferReference ?? payout.Id.ToString("N"),
                    Metadata: null),
                cancellationToken);

            var accepted = string.Equals(result.ProviderStatus, "queued", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.ProviderStatus, "accepted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.ProviderStatus, "processing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.ProviderStatus, "paid", StringComparison.OrdinalIgnoreCase);

            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
                accepted ? PayoutStatus.Queued : PayoutStatus.Failed,
                providerName: result.ProviderName,
                providerTransferId: result.ProviderTransferId,
                transferReference: payout.TransferReference,
                failureReason: result.FailureMessage,
                rawPayload: result.RawResponse));

            if (accepted)
            {
                payout.MarkQueued(result.ProviderTransferId);
            }
            else
            {
                payout.MarkAsFailed(result.FailureMessage);
                payout.Settlement.MarkPayoutFailed();
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (!accepted)
            {
                await SendPayoutFailedAlertAsync(payout, result.FailureMessage, cancellationToken);
            }

            return payout;
        }
        catch (Exception ex)
        {
            payout.MarkAsFailed(ex.Message);
            payout.Settlement.MarkPayoutFailed();
            await _context.SaveChangesAsync(cancellationToken);
            await SendPayoutIntegrationFailureAlertAsync(payout, ex, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Marks a payout as paid. Used by:
    /// <list type="bullet">
    ///   <item>Provider webhooks once a real <see cref="IPayoutGateway"/> reports success.</item>
    ///   <item>Admin "manual mark paid" actions when payouts run outside of any gateway.</item>
    /// </list>
    /// Posts the journal entry and updates the wallet projection.
    /// </summary>
    public async Task<Payout> MarkPaidAsync(
        Guid payoutId,
        string transferReference,
        string? providerTransferId = null,
        CancellationToken cancellationToken = default)
    {
        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);

        if (payout.Status == PayoutStatus.Paid)
        {
            return payout;
        }

        payout.MarkAsPaid(transferReference);
        payout.Settlement.MarkPaidOut();
        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ProviderCallback,
            PayoutStatus.Paid,
            providerName: payout.ProviderName,
            providerTransferId: providerTransferId ?? payout.ProviderTransferId,
            transferReference: transferReference,
            failureReason: null,
            rawPayload: null));

        await PostPayoutPaidAsync(payout, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
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
            currencyCode: "SAR",
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
                "فشل تكامل تحويل التسوية",
                "Payout integration failure",
                $"حدث خطأ أثناء إرسال تحويل للتسوية {payout.SettlementId}.",
                $"Payout trigger failed for settlement {payout.SettlementId}.",
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
