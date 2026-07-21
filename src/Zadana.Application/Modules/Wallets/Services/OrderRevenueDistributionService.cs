using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finance.Services;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Wallets.Services;

public class OrderRevenueDistributionService
{
    private readonly IApplicationDbContext _context;
    private readonly FinancialSettingsOptions _settings;
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
        VendorRecoveryService? vendorRecoveryService = null,
        PayoutOrchestrator? payoutOrchestrator = null)
    {
        _context = context;
        _settings = settings.Value;
        _financialEventPostingService = financialEventPostingService;
        _walletProjectionUpdater = walletProjectionUpdater;
        _logger = logger;
        _vendorRecoveryService = vendorRecoveryService;
        _ = vendorPayoutWalletService;
        _ = payoutOrchestrator;
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
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

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

        // 4. Confirm the vendor exists. Payout timing is handled by the
        // scheduled settlement worker, never from this per-order path.
        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == order.VendorId)
            .Select(v => new
            {
                v.Id
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
        var legacyDriverCommission = ResolveLegacyDriverCommission(order);
        var initialDistribution = RevenueDistributionCalculator.Compute(
            order,
            legacyDriverCommissionAmount: legacyDriverCommission);
        var vendorRecoveryApplied = 0m;

        if (_vendorRecoveryService is not null && initialDistribution.VendorNet > 0m)
        {
            vendorRecoveryApplied = await _vendorRecoveryService.ApplyOutstandingRecoveriesAsync(
                vendor.Id,
                orderId,
                initialDistribution.VendorNet,
                cancellationToken);
        }

        var distribution = RevenueDistributionCalculator.Compute(
            order,
            vendorRecoveryApplied,
            legacyDriverCommission);

        _logger.LogInformation(
            "[RevenueDistribution] Order {OrderId}: VendorNet={VendorNet}, DriverNet={DriverNet}, PlatformNet={PlatformNet}, TaxPayable={TaxPayable}",
            orderId,
            distribution.VendorNet,
            distribution.DriverNet,
            distribution.PlatformRevenue,
            distribution.TaxPayable);

        // 7. Post ledger-first journal, then update wallet projections from the posted entry.
        var postingLines = BuildDeliveredOrderPostingLines(
            orderId,
            order.UserId,
            vendor.Id,
            driverAssignment?.DriverId,
            distribution,
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

        // Vendor revenue remains in the wallet until its scheduled settlement.
        // Legacy per-order flags are intentionally not allowed to trigger payouts.

        _logger.LogInformation("[RevenueDistribution] Order {OrderId} distributed successfully.", orderId);
    }

    private IReadOnlyCollection<JournalLineDraft> BuildDeliveredOrderPostingLines(
        Guid orderId,
        Guid customerId,
        Guid vendorId,
        Guid? driverId,
        RevenueDistribution distribution,
        PaymentMethodType paymentMethod)
    {
        var lines = new List<JournalLineDraft>();
        var driverNet = distribution.DriverNet;
        var platformRevenue = distribution.PlatformRevenue;
        if (driverNet > 0 && driverId is null)
        {
            _logger.LogWarning(
                "[RevenueDistribution] Order {OrderId} has no assigned driver; moving driver net {DriverNet} to platform revenue.",
                orderId,
                driverNet);
            platformRevenue += driverNet;
            driverNet = 0m;
        }

        var postingTotal = distribution.VendorNet + driverNet + platformRevenue + distribution.TaxPayable;

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
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.CustomerAdvance,
                postingTotal,
                0m,
                FinancialOwnerType.Customer,
                customerId,
                orderId,
                Memo: $"Customer advance cleared for order {orderId}"));
        }

        if (distribution.VendorNet > 0)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.VendorPayable,
                0m,
                distribution.VendorNet,
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

        if (platformRevenue > 0)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.PlatformRevenue,
                0m,
                platformRevenue,
                FinancialOwnerType.Platform,
                _settings.PlatformWalletOwnerId,
                orderId,
                Memo: $"Platform revenue for order {orderId}"));
        }

        if (distribution.TaxPayable > 0)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.TaxPayable,
                0m,
                distribution.TaxPayable,
                FinancialOwnerType.Platform,
                _settings.PlatformWalletOwnerId,
                orderId,
                Memo: $"Tax payable for order {orderId}"));
        }

        return lines;
    }

    private decimal ResolveLegacyDriverCommission(Order order)
    {
        if (order.DriverCommissionAmount > 0m || !string.IsNullOrWhiteSpace(order.CommissionPolicySnapshot))
        {
            return order.DriverCommissionAmount;
        }

        return decimal.Round(
            Math.Max(order.DeliveryFee, 0m) * _settings.DriverCommissionRatePercent / 100m,
            2,
            MidpointRounding.AwayFromZero);
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
