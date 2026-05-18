using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Wallets.Services;

public class OrderRevenueDistributionService
{
    private readonly IApplicationDbContext _context;
    private readonly FinancialSettingsOptions _settings;
    private readonly VendorPayoutWalletService _vendorPayoutWalletService;
    private readonly VendorRecoveryService? _vendorRecoveryService;
    private readonly FinancialEventPostingService _financialEventPostingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly ILogger<OrderRevenueDistributionService> _logger;

    public OrderRevenueDistributionService(
        IApplicationDbContext context,
        IOptions<FinancialSettingsOptions> settings,
        VendorPayoutWalletService vendorPayoutWalletService,
        FinancialEventPostingService financialEventPostingService,
        WalletProjectionUpdater walletProjectionUpdater,
        ILogger<OrderRevenueDistributionService> logger,
        VendorRecoveryService? vendorRecoveryService = null)
    {
        _context = context;
        _settings = settings.Value;
        _vendorPayoutWalletService = vendorPayoutWalletService;
        _financialEventPostingService = financialEventPostingService;
        _walletProjectionUpdater = walletProjectionUpdater;
        _logger = logger;
        _vendorRecoveryService = vendorRecoveryService;
    }

    /// <summary>
    /// Distributes revenue for an eligible delivered order across Vendor, Driver, and Platform wallets.
    /// Idempotent: skips if already distributed.
    /// </summary>
    public virtual async Task DistributeAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // 1. Load order
        var order = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.VendorId,
                o.Status,
                o.PaymentMethod,
                o.PaymentStatus,
                o.TotalAmount,
                o.DeliveryFee,
                o.CommissionAmount
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("[RevenueDistribution] Order {OrderId} not found.", orderId);
            return;
        }

        // 2. Check eligibility
        if (!IsEligible(order.Status, order.PaymentMethod, order.PaymentStatus))
        {
            _logger.LogInformation(
                "[RevenueDistribution] Order {OrderId} not eligible. Status={Status}, PaymentMethod={PaymentMethod}, PaymentStatus={PaymentStatus}",
                orderId, order.Status, order.PaymentMethod, order.PaymentStatus);
            return;
        }

        // 3. Idempotency: check if already distributed
        var idempotencyKey = $"order-revenue:{orderId:N}";
        var alreadyDistributed = await _context.FinancialEvents
            .AsNoTracking()
            .AnyAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

        if (alreadyDistributed)
        {
            _logger.LogInformation("[RevenueDistribution] Order {OrderId} already distributed. Skipping.", orderId);
            return;
        }

        // 4. Load vendor financial mode
        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == order.VendorId)
            .Select(v => new
            {
                v.Id,
                v.FinancialLifecycleMode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendor is null)
        {
            _logger.LogWarning("[RevenueDistribution] Vendor {VendorId} not found for order {OrderId}.", order.VendorId, orderId);
            return;
        }

        // 5. Find assigned driver
        var driverAssignment = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(a => a.OrderId == orderId && a.DriverId != null)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new { a.DriverId })
            .FirstOrDefaultAsync(cancellationToken);

        // 6. Calculate revenue split
        var driverCommissionRate = _settings.DriverCommissionRatePercent;

        var vendorGross = order.TotalAmount - order.DeliveryFee;
        var vendorCommission = Math.Max(order.CommissionAmount, 0m);
        var vendorNet = vendorGross - vendorCommission;

        var driverGross = order.DeliveryFee;
        var driverCommission = Math.Round(driverGross * driverCommissionRate / 100m, 2);
        var driverNet = driverGross - driverCommission;

        var platformNet = vendorCommission + driverCommission;

        if (_vendorRecoveryService is not null && vendorNet > 0m)
        {
            var recoveredFromOutstanding = await _vendorRecoveryService.ApplyOutstandingRecoveriesAsync(
                vendor.Id,
                orderId,
                vendorNet,
                cancellationToken);

            if (recoveredFromOutstanding > 0m)
            {
                vendorNet -= recoveredFromOutstanding;
                platformNet += recoveredFromOutstanding;
            }
        }

        _logger.LogInformation(
            "[RevenueDistribution] Order {OrderId}: VendorNet={VendorNet}, DriverNet={DriverNet}, PlatformNet={PlatformNet}",
            orderId, vendorNet, driverNet, platformNet);

        // 7. Post ledger-first journal, then update wallet projections from the posted entry.
        var postingLines = BuildDeliveredOrderPostingLines(
            orderId,
            vendor.Id,
            driverAssignment?.DriverId,
            vendorNet,
            driverNet,
            platformNet,
            order.PaymentMethod);

        if (postingLines.Count == 0)
        {
            _logger.LogWarning("[RevenueDistribution] Order {OrderId} produced no financial posting lines.", orderId);
            return;
        }

        var postingResult = await _financialEventPostingService.PostAsync(
            order.PaymentMethod == PaymentMethodType.CashOnDelivery
                ? FinancialEventType.CodCashCollected
                : FinancialEventType.OnlinePaymentDelivered,
            idempotencyKey,
            postingLines,
            orderId: orderId,
            description: $"Revenue distribution for order {order.OrderNumber}",
            cancellationToken: cancellationToken);

        await _walletProjectionUpdater.ApplyJournalEntryAsync(postingResult.JournalEntryId, cancellationToken);

        // 8. Handle per-order direct payout for vendor
        if (vendor.FinancialLifecycleMode == VendorFinancialLifecycleMode.PerOrderDirectPayout && vendorNet > 0)
        {
            await CreateDirectPayoutAsync(vendor.Id, orderId, order, vendorNet, driverNet, platformNet, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("[RevenueDistribution] Order {OrderId} distributed successfully.", orderId);
    }

    private IReadOnlyCollection<JournalLineDraft> BuildDeliveredOrderPostingLines(
        Guid orderId,
        Guid vendorId,
        Guid? driverId,
        decimal vendorNet,
        decimal driverNet,
        decimal platformNet,
        PaymentMethodType paymentMethod)
    {
        var lines = new List<JournalLineDraft>();
        var postingTotal = vendorNet + platformNet;

        if (driverNet > 0 && driverId is not null)
        {
            postingTotal += driverNet;
        }

        if (postingTotal <= 0)
        {
            return lines;
        }

        if (paymentMethod == PaymentMethodType.CashOnDelivery)
        {
            if (driverId is null)
            {
                _logger.LogWarning("[RevenueDistribution] COD order {OrderId} has no assigned driver; posting skipped.", orderId);
                return [];
            }

            lines.Add(new JournalLineDraft(
                FinancialAccountCode.DriverCodReceivable,
                postingTotal,
                0m,
                FinancialOwnerType.Driver,
                driverId,
                orderId,
                Memo: $"COD cash collected for order {orderId}"));
        }
        else
        {
            // For online orders with a prior OnlinePaymentCaptured event, debit
            // CustomerAdvance (the liability recognised at capture time) instead
            // of GatewayReceivable. This keeps the ledger consistent with the
            // revised SAR-only workflow (section 8.2). For legacy orders without
            // a capture event, fall back to GatewayReceivable for backward compat.
            var hasCaptureEvent = _context.FinancialEvents
                .AsNoTracking()
                .Any(e => e.OrderId == orderId && e.EventType == FinancialEventType.OnlinePaymentCaptured);

            var debitAccount = hasCaptureEvent
                ? FinancialAccountCode.CustomerAdvance
                : FinancialAccountCode.GatewayReceivable;

            var debitOwnerType = hasCaptureEvent
                ? FinancialOwnerType.Customer
                : FinancialOwnerType.Gateway;

            lines.Add(new JournalLineDraft(
                debitAccount,
                postingTotal,
                0m,
                debitOwnerType,
                null,
                orderId,
                Memo: hasCaptureEvent
                    ? $"Customer advance cleared on delivery for order {orderId}"
                    : $"Online payment receivable for order {orderId}"));
        }

        if (vendorNet > 0)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.VendorPayable,
                0m,
                vendorNet,
                FinancialOwnerType.Vendor,
                vendorId,
                orderId,
                Memo: $"Vendor payable for order {orderId}"));
        }

        if (driverNet > 0 && driverId is not null)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.DriverPayable,
                0m,
                driverNet,
                FinancialOwnerType.Driver,
                driverId,
                orderId,
                Memo: $"Driver payable for order {orderId}"));
        }

        if (platformNet > 0)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.PlatformRevenue,
                0m,
                platformNet,
                FinancialOwnerType.Platform,
                _settings.PlatformWalletOwnerId,
                orderId,
                Memo: $"Platform revenue for order {orderId}"));
        }

        return lines;
    }

    private async Task CreateDirectPayoutAsync(
        Guid vendorId,
        Guid orderId,
        dynamic order,
        decimal vendorNet,
        decimal driverNet,
        decimal platformNet,
        CancellationToken cancellationToken)
    {
        var primaryBankAccount = await _context.VendorBankAccounts
            .AsNoTracking()
            .Where(b => b.VendorId == vendorId)
            .OrderByDescending(b => b.IsPrimary)
            .ThenByDescending(b => b.VerifiedAtUtc)
            .ThenByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryBankAccount is null)
        {
            _logger.LogWarning("[RevenueDistribution] No bank account for vendor {VendorId}. Payout skipped.", vendorId);
            return;
        }

        var settlement = new Settlement(vendorId, null, SettlementOrigin.DirectPerOrder);
        settlement.UpdateTotals(order.TotalAmount - order.DeliveryFee, order.CommissionAmount);
        _context.Settlements.Add(settlement);

        _context.SettlementItems.Add(new SettlementItem(
            settlement.Id, orderId, vendorNet, driverNet, platformNet,
            order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0m));

        var payout = new Payout(settlement.Id, vendorNet, primaryBankAccount.Id);
        _context.Payouts.Add(payout);

        await _vendorPayoutWalletService.EnsureHoldAsync(
            vendorId,
            settlement.Id,
            vendorNet,
            "PayoutHold",
            $"Hold for direct payout on order {orderId}",
            cancellationToken);
    }

    private static bool IsEligible(OrderStatus status, PaymentMethodType paymentMethod, PaymentStatus paymentStatus)
    {
        if (status != OrderStatus.Delivered)
            return false;

        return paymentMethod switch
        {
            PaymentMethodType.CashOnDelivery => paymentStatus is PaymentStatus.Collected or PaymentStatus.Paid,
            _ => paymentStatus == PaymentStatus.Paid
        };
    }
}
