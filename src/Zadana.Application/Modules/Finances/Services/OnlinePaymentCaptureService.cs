using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Posts the <see cref="FinancialEventType.OnlinePaymentCaptured"/> ledger event
/// when an online (Moyasar / future provider) payment reaches a captured state.
/// <para>
/// Captured posting (per section 8.1 of the revised SAR-only workflow):
/// <code>
/// Dr GatewayReceivable      Order.TotalAmount
/// Cr CustomerAdvance        Order.TotalAmount
/// </code>
/// </para>
/// <para>
/// At delivery time, the customer-advance liability is cleared and re-credited
/// to vendor / driver / platform / tax via <see cref="Wallets.Services.OrderRevenueDistributionService"/>.
/// </para>
/// </summary>
public sealed class OnlinePaymentCaptureService
{
    private readonly IApplicationDbContext _context;
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly FinancialSettingsOptions _settings;
    private readonly ILogger<OnlinePaymentCaptureService> _logger;

    public OnlinePaymentCaptureService(
        IApplicationDbContext context,
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater,
        ILogger<OnlinePaymentCaptureService> logger,
        IOptions<FinancialSettingsOptions>? settings = null)
    {
        _context = context;
        _postingService = postingService;
        _walletProjectionUpdater = walletProjectionUpdater;
        _settings = settings?.Value ?? new FinancialSettingsOptions();
        _logger = logger;
    }

    /// <summary>
    /// Idempotency key used by callers and the read side. Public so other services
    /// (e.g. <c>OrderRevenueDistributionService</c>) can detect whether a captured
    /// event already exists for an order before deciding which debit account to use.
    /// </summary>
    public static string BuildIdempotencyKey(string providerName, string providerPaymentId) =>
        $"payment-captured:{providerName.Trim().ToLowerInvariant()}:{providerPaymentId.Trim()}";

    public async Task PostCapturedAsync(
        Order order,
        Payment payment,
        string providerName,
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(payment);

        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(providerPaymentId))
        {
            _logger.LogWarning(
                "[OnlinePaymentCapture] Skipping capture posting for order {OrderId}: provider={Provider} providerPaymentId={ProviderPaymentId}",
                order.Id, providerName, providerPaymentId);
            return;
        }

        if (order.TotalAmount <= 0m)
        {
            _logger.LogInformation(
                "[OnlinePaymentCapture] Order {OrderId} total is zero, no capture posting needed.",
                order.Id);
            return;
        }

        // The Order.Currency snapshot was filled at creation; for legacy orders the
        // value may still be EGP. The capture posting itself is currency-agnostic
        // beyond the SAR-only policy, so just enforce the policy and bail on legacy
        // currencies rather than rewriting historical financial state.
        if (!CurrencyPolicy.IsOfficial(order.Currency))
        {
            _logger.LogWarning(
                "[OnlinePaymentCapture] Order {OrderId} currency {Currency} is not SAR; skipping capture posting.",
                order.Id, order.Currency);
            return;
        }

        var idempotencyKey = BuildIdempotencyKey(providerName, providerPaymentId);
        var alreadyPosted = await _context.FinancialEvents
            .AsNoTracking()
            .AnyAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

        if (alreadyPosted)
        {
            _logger.LogInformation(
                "[OnlinePaymentCapture] Order {OrderId} payment {PaymentId} already captured (idempotency hit).",
                order.Id, payment.Id);
            return;
        }

        var lines = new List<JournalLineDraft>
        {
            new(
                FinancialAccountCode.GatewayReceivable,
                DebitAmount: order.TotalAmount,
                CreditAmount: 0m,
                OwnerType: FinancialOwnerType.Gateway,
                OwnerId: null,
                OrderId: order.Id,
                Memo: $"Gateway receivable on payment captured for order {order.OrderNumber}"),
            new(
                FinancialAccountCode.CustomerAdvance,
                DebitAmount: 0m,
                CreditAmount: order.TotalAmount,
                OwnerType: FinancialOwnerType.Customer,
                OwnerId: order.UserId,
                OrderId: order.Id,
                Memo: $"Customer advance recognised for order {order.OrderNumber}"),
        };

        var gatewayFee = decimal.Round(
            (order.TotalAmount * _settings.GatewayFeeRatePercent / 100m) + _settings.GatewayFeeFixedAmount,
            2,
            MidpointRounding.AwayFromZero);

        if (gatewayFee > 0m)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.GatewayFeeExpense,
                DebitAmount: gatewayFee,
                CreditAmount: 0m,
                OwnerType: FinancialOwnerType.Platform,
                OwnerId: _settings.PlatformWalletOwnerId,
                OrderId: order.Id,
                Memo: $"Gateway capture fee for order {order.OrderNumber}"));
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.GatewayReceivable,
                DebitAmount: 0m,
                CreditAmount: gatewayFee,
                OwnerType: FinancialOwnerType.Gateway,
                OwnerId: null,
                OrderId: order.Id,
                Memo: $"Gateway receivable fee offset for order {order.OrderNumber}"));
        }

        var posting = await _postingService.PostAsync(
            FinancialEventType.OnlinePaymentCaptured,
            idempotencyKey,
            lines,
            orderId: order.Id,
            currencyCode: CurrencyPolicy.OfficialCurrency,
            description: $"Online payment captured for order {order.OrderNumber} via {providerName}",
            cancellationToken: cancellationToken);

        if (!posting.WasAlreadyPosted)
        {
            await _walletProjectionUpdater.ApplyJournalEntryAsync(posting.JournalEntryId, cancellationToken);
            _logger.LogInformation(
                "[OnlinePaymentCapture] Posted capture event for order {OrderId} payment {PaymentId} via {Provider}.",
                order.Id, payment.Id, providerName);
        }
    }
}
