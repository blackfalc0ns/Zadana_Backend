using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
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
    private readonly ILogger<OrderRevenueDistributionService> _logger;

    public OrderRevenueDistributionService(
        IApplicationDbContext context,
        IOptions<FinancialSettingsOptions> settings,
        VendorPayoutWalletService vendorPayoutWalletService,
        ILogger<OrderRevenueDistributionService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _vendorPayoutWalletService = vendorPayoutWalletService;
        _logger = logger;
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
        var alreadyDistributed = await _context.WalletTransactions
            .AsNoTracking()
            .AnyAsync(t => t.OrderId == orderId && t.TxnType == WalletTxnType.OrderRevenue, cancellationToken);

        if (alreadyDistributed)
        {
            _logger.LogInformation("[RevenueDistribution] Order {OrderId} already distributed. Skipping.", orderId);
            return;
        }

        // 4. Load vendor commission rate
        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == order.VendorId)
            .Select(v => new
            {
                v.Id,
                v.CommissionRate,
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
        var vendorCommissionRate = vendor.CommissionRate ?? 0m;
        var driverCommissionRate = _settings.DriverCommissionRatePercent;

        var vendorGross = order.TotalAmount - order.DeliveryFee;
        var vendorCommission = Math.Round(vendorGross * vendorCommissionRate / 100m, 2);
        var vendorNet = vendorGross - vendorCommission;

        var driverGross = order.DeliveryFee;
        var driverCommission = Math.Round(driverGross * driverCommissionRate / 100m, 2);
        var driverNet = driverGross - driverCommission;

        var platformNet = vendorCommission + driverCommission;

        _logger.LogInformation(
            "[RevenueDistribution] Order {OrderId}: VendorNet={VendorNet}, DriverNet={DriverNet}, PlatformNet={PlatformNet}",
            orderId, vendorNet, driverNet, platformNet);

        // 7. Get or create wallets
        var vendorWallet = await GetOrCreateWalletAsync(WalletOwnerType.Vendor, vendor.Id, cancellationToken);
        var platformWallet = await GetOrCreateWalletAsync(WalletOwnerType.Platform, _settings.PlatformWalletOwnerId, cancellationToken);

        // 8. Credit vendor wallet
        if (vendorNet > 0)
        {
            vendorWallet.Credit(vendorNet);
            var vendorTxn = new WalletTransaction(
                vendorWallet.Id, WalletTxnType.OrderRevenue, vendorNet, "IN",
                orderId: orderId,
                referenceType: "OrderRevenue",
                description: $"Revenue from order {orderId}");
            _context.WalletTransactions.Add(vendorTxn);
        }

        // 9. Credit driver wallet (if assigned)
        if (driverNet > 0 && driverAssignment?.DriverId != null)
        {
            var driverWallet = await GetOrCreateWalletAsync(WalletOwnerType.Driver, driverAssignment.DriverId.Value, cancellationToken);
            driverWallet.Credit(driverNet);
            _context.WalletTransactions.Add(new WalletTransaction(
                driverWallet.Id, WalletTxnType.OrderRevenue, driverNet, "IN",
                orderId: orderId,
                referenceType: "OrderRevenue",
                description: $"Delivery fee from order {orderId}"));
        }

        // 10. Credit platform wallet
        if (platformNet > 0)
        {
            platformWallet.Credit(platformNet);
            _context.WalletTransactions.Add(new WalletTransaction(
                platformWallet.Id, WalletTxnType.OrderRevenue, platformNet, "IN",
                orderId: orderId,
                referenceType: "OrderRevenue",
                description: $"Commission from order {orderId}"));
        }

        // 11. Handle per-order direct payout for vendor
        if (vendor.FinancialLifecycleMode == VendorFinancialLifecycleMode.PerOrderDirectPayout && vendorNet > 0)
        {
            await CreateDirectPayoutAsync(vendor.Id, orderId, order, vendorNet, driverNet, platformNet, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[RevenueDistribution] Order {OrderId} distributed successfully.", orderId);
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

    private async Task<Wallet> GetOrCreateWalletAsync(
        WalletOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == ownerType && w.OwnerId == ownerId, cancellationToken);

        if (wallet is null)
        {
            wallet = new Wallet(ownerType, ownerId);
            _context.Wallets.Add(wallet);
        }

        return wallet;
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
