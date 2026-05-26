using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finance.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class RevenueReconciliationService
{
    private const int DefaultMaxOrders = 500;
    private const decimal MoneyTolerance = 0.009m;

    private readonly IApplicationDbContext _context;
    private readonly FinancialSettingsOptions _settings;
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;

    public RevenueReconciliationService(
        IApplicationDbContext context,
        IOptions<FinancialSettingsOptions> settings,
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater)
    {
        _context = context;
        _settings = settings.Value;
        _postingService = postingService;
        _walletProjectionUpdater = walletProjectionUpdater;
    }

    public async Task<RevenueReconciliationPreviewDto> PreviewAsync(
        int maxOrders = DefaultMaxOrders,
        CancellationToken cancellationToken = default)
    {
        var plans = await BuildPlansAsync(maxOrders, cancellationToken);
        return ToPreviewDto(plans);
    }

    public async Task<RevenueReconciliationApplyResultDto> ApplyAsync(
        int maxOrders = DefaultMaxOrders,
        CancellationToken cancellationToken = default)
    {
        var plans = await BuildPlansAsync(maxOrders, cancellationToken);
        var journalEntryIds = new List<Guid>();

        foreach (var plan in plans.Where(item => item.CanApply && item.AdjustmentLines.Count > 0))
        {
            var posting = await _postingService.PostAsync(
                FinancialEventType.FinancialAdjustmentApplied,
                BuildIdempotencyKey(plan.OrderId, plan.AdjustmentLines),
                plan.AdjustmentLines,
                orderId: plan.OrderId,
                currencyCode: CurrencyPolicy.OfficialCurrency,
                description: $"Revenue reconciliation adjustment for order {plan.OrderNumber}",
                cancellationToken: cancellationToken);

            await _walletProjectionUpdater.ApplyJournalEntryAsync(posting.JournalEntryId, cancellationToken);
            journalEntryIds.Add(posting.JournalEntryId);
        }

        var preview = ToPreviewDto(plans);
        return new RevenueReconciliationApplyResultDto(
            preview.OrdersChecked,
            preview.AffectedOrders,
            journalEntryIds.Count,
            journalEntryIds,
            preview.TotalsByAccount,
            preview.Orders);
    }

    private async Task<IReadOnlyList<OrderReconciliationPlan>> BuildPlansAsync(
        int maxOrders,
        CancellationToken cancellationToken)
    {
        maxOrders = Math.Clamp(maxOrders, 1, 10_000);

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Delivered)
            .Where(order =>
                (order.PaymentMethod == PaymentMethodType.CashOnDelivery &&
                    (order.PaymentStatus == PaymentStatus.Collected ||
                     order.PaymentStatus == PaymentStatus.Paid ||
                     order.PaymentStatus == PaymentStatus.Settled)) ||
                (order.PaymentMethod != PaymentMethodType.CashOnDelivery &&
                    (order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Settled)))
            .OrderBy(order => order.DeliveredAtUtc ?? order.UpdatedAtUtc)
            .Take(maxOrders)
            .ToListAsync(cancellationToken);

        var plans = new List<OrderReconciliationPlan>();
        foreach (var order in orders)
        {
            try
            {
                plans.Add(await BuildPlanAsync(order, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                plans.Add(OrderReconciliationPlan.Skipped(
                    order.Id,
                    order.OrderNumber,
                    order.PaymentMethod.ToString(),
                    order.TotalAmount,
                    $"Unable to calculate expected distribution: {ex.Message}"));
            }
        }

        return plans;
    }

    private async Task<OrderReconciliationPlan> BuildPlanAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var driverId = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id && item.DriverId.HasValue)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.DriverId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order.PaymentMethod == PaymentMethodType.CashOnDelivery && driverId is null)
        {
            return OrderReconciliationPlan.Skipped(
                order.Id,
                order.OrderNumber,
                order.PaymentMethod.ToString(),
                order.TotalAmount,
                "COD order has no assigned driver.");
        }

        var expected = BuildExpectedDeliveredLines(order, driverId);
        var current = await LoadCurrentDeliveredLinesAsync(order.Id, cancellationToken);
        var adjustmentLines = BuildAdjustmentLines(order.Id, expected, current);

        if (RequiresCustomerAdvanceFunding(order, adjustmentLines) &&
            !await HasNonCashFundingSourceAsync(order.Id, current, cancellationToken))
        {
            return OrderReconciliationPlan.Skipped(
                order.Id,
                order.OrderNumber,
                order.PaymentMethod.ToString(),
                order.TotalAmount,
                BuildMissingFundingSkipReason(order.PaymentMethod));
        }

        return new OrderReconciliationPlan(
            order.Id,
            order.OrderNumber,
            order.PaymentMethod.ToString(),
            order.TotalAmount,
            true,
            null,
            adjustmentLines);
    }

    private async Task<bool> HasNonCashFundingSourceAsync(
        Guid orderId,
        IReadOnlyCollection<JournalLineDraft> currentDeliveredLines,
        CancellationToken cancellationToken)
    {
        if (currentDeliveredLines.Any(line =>
                line.AccountCode == FinancialAccountCode.GatewayReceivable &&
                line.DebitAmount > line.CreditAmount))
        {
            return true;
        }

        return await _context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.FinancialEvent)
            .Include(entry => entry.Lines)
            .AnyAsync(entry =>
                entry.FinancialEvent.OrderId == orderId &&
                (entry.FinancialEvent.EventType == FinancialEventType.OnlinePaymentCaptured ||
                 entry.FinancialEvent.EventType == FinancialEventType.BankTransferConfirmed) &&
                entry.Lines.Any(line =>
                    line.AccountCode == FinancialAccountCode.CustomerAdvance &&
                    line.CreditAmount > line.DebitAmount),
                cancellationToken);
    }

    private static bool RequiresCustomerAdvanceFunding(
        Order order,
        IReadOnlyCollection<JournalLineDraft> adjustmentLines)
    {
        return order.PaymentMethod != PaymentMethodType.CashOnDelivery &&
            adjustmentLines.Any(line =>
                line.AccountCode == FinancialAccountCode.CustomerAdvance &&
                line.DebitAmount > line.CreditAmount);
    }

    private static string BuildMissingFundingSkipReason(PaymentMethodType paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethodType.Wallet =>
                "Wallet order has no supported wallet-capture ledger funding source to clear. Standalone wallet checkout is not supported yet.",
            PaymentMethodType.Mada =>
                "Mada order has no CustomerAdvance funding or legacy GatewayReceivable source to clear. Mada should be captured through the card gateway.",
            PaymentMethodType.ApplePay =>
                "Apple Pay order has no CustomerAdvance funding or legacy GatewayReceivable source to clear. Apple Pay checkout is not enabled yet.",
            _ => "Non-cash order has no CustomerAdvance funding or legacy GatewayReceivable source to clear."
        };

    private IReadOnlyCollection<JournalLineDraft> BuildExpectedDeliveredLines(Order order, Guid? driverId)
    {
        var legacyDriverCommission = ResolveLegacyDriverCommission(order);
        var distribution = RevenueDistributionCalculator.Compute(
            order,
            legacyDriverCommissionAmount: legacyDriverCommission);

        var driverNet = distribution.DriverNet;
        var platformRevenue = distribution.PlatformRevenue;
        if (driverNet > 0m && driverId is null)
        {
            platformRevenue += driverNet;
            driverNet = 0m;
        }

        var total = distribution.VendorNet + driverNet + platformRevenue + distribution.TaxPayable;
        var lines = new List<JournalLineDraft>();

        if (order.PaymentMethod == PaymentMethodType.CashOnDelivery)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.DriverCodReceivable,
                total,
                0m,
                FinancialOwnerType.Driver,
                driverId,
                order.Id,
                Memo: $"Expected COD receivable for order {order.OrderNumber}"));
        }
        else
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.CustomerAdvance,
                total,
                0m,
                FinancialOwnerType.Customer,
                order.UserId,
                order.Id,
                Memo: $"Expected customer advance clearing for order {order.OrderNumber}"));
        }

        if (distribution.VendorNet > 0m)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.VendorPayable,
                0m,
                distribution.VendorNet,
                FinancialOwnerType.Vendor,
                order.VendorId,
                order.Id));
        }

        if (driverNet > 0m && driverId.HasValue)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.DriverPayable,
                0m,
                driverNet,
                FinancialOwnerType.Driver,
                driverId,
                order.Id));
        }

        if (platformRevenue > 0m)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.PlatformRevenue,
                0m,
                platformRevenue,
                FinancialOwnerType.Platform,
                _settings.PlatformWalletOwnerId,
                order.Id));
        }

        if (distribution.TaxPayable > 0m)
        {
            lines.Add(new JournalLineDraft(
                FinancialAccountCode.TaxPayable,
                0m,
                distribution.TaxPayable,
                FinancialOwnerType.Platform,
                _settings.PlatformWalletOwnerId,
                order.Id));
        }

        return lines;
    }

    private async Task<IReadOnlyCollection<JournalLineDraft>> LoadCurrentDeliveredLinesAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var reconciliationPrefix = $"revenue-reconciliation:{orderId:N}:";

        var entries = await _context.JournalEntries
            .AsNoTracking()
            .Include(item => item.FinancialEvent)
            .Include(item => item.Lines)
            .Where(item => item.FinancialEvent.OrderId == orderId)
            .Where(item =>
                item.FinancialEvent.EventType == FinancialEventType.CodCashCollected ||
                item.FinancialEvent.EventType == FinancialEventType.OnlinePaymentDelivered ||
                item.FinancialEvent.EventType == FinancialEventType.OnlineOrderDelivered ||
                (item.FinancialEvent.EventType == FinancialEventType.FinancialAdjustmentApplied &&
                    item.FinancialEvent.IdempotencyKey.StartsWith(reconciliationPrefix)))
            .ToListAsync(cancellationToken);

        return entries
            .SelectMany(entry => entry.Lines)
            .Select(line => new JournalLineDraft(
                line.AccountCode,
                line.DebitAmount,
                line.CreditAmount,
                line.OwnerType,
                line.OwnerId,
                line.OrderId,
                line.SettlementId,
                line.PayoutId,
                line.Memo))
            .ToList();
    }

    private static IReadOnlyCollection<JournalLineDraft> BuildAdjustmentLines(
        Guid orderId,
        IReadOnlyCollection<JournalLineDraft> expectedLines,
        IReadOnlyCollection<JournalLineDraft> currentLines)
    {
        var expected = SumByAccount(expectedLines);
        var current = SumByAccount(currentLines);
        var keys = expected.Keys.Concat(current.Keys).Distinct().OrderBy(item => item.AccountCode).ThenBy(item => item.OwnerType).ThenBy(item => item.OwnerId).ToList();
        var lines = new List<JournalLineDraft>();

        foreach (var key in keys)
        {
            expected.TryGetValue(key, out var expectedNet);
            current.TryGetValue(key, out var currentNet);
            var delta = RoundMoney(expectedNet - currentNet);
            if (Math.Abs(delta) <= MoneyTolerance)
            {
                continue;
            }

            lines.Add(new JournalLineDraft(
                key.AccountCode,
                DebitAmount: delta > 0m ? delta : 0m,
                CreditAmount: delta < 0m ? Math.Abs(delta) : 0m,
                OwnerType: key.OwnerType,
                OwnerId: key.OwnerId,
                OrderId: orderId,
                Memo: $"Revenue reconciliation delta for order {orderId}"));
        }

        return lines;
    }

    private static IReadOnlyDictionary<AccountKey, decimal> SumByAccount(IReadOnlyCollection<JournalLineDraft> lines)
    {
        return lines
            .GroupBy(line => new AccountKey(line.AccountCode, line.OwnerType, line.OwnerId))
            .ToDictionary(
                group => group.Key,
                group => RoundMoney(group.Sum(line => line.DebitAmount - line.CreditAmount)));
    }

    private RevenueReconciliationPreviewDto ToPreviewDto(IReadOnlyList<OrderReconciliationPlan> plans)
    {
        var affected = plans.Where(item => item.CanApply && item.AdjustmentLines.Count > 0).ToList();
        var visible = plans.Where(item => item.AdjustmentLines.Count > 0 || !item.CanApply).ToList();
        var totals = affected
            .SelectMany(item => item.AdjustmentLines)
            .GroupBy(item => item.AccountCode.ToString())
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => RoundMoney(group.Sum(line => line.DebitAmount - line.CreditAmount)));

        return new RevenueReconciliationPreviewDto(
            plans.Count,
            affected.Count,
            totals,
            visible.Select(ToOrderDeltaDto).ToList());
    }

    private static RevenueReconciliationOrderDeltaDto ToOrderDeltaDto(OrderReconciliationPlan plan)
    {
        var accountDeltas = plan.AdjustmentLines
            .GroupBy(item => item.AccountCode.ToString())
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => RoundMoney(group.Sum(line => line.DebitAmount - line.CreditAmount)));

        return new RevenueReconciliationOrderDeltaDto(
            plan.OrderId,
            plan.OrderNumber,
            plan.PaymentMethod,
            plan.TotalAmount,
            accountDeltas,
            plan.CanApply,
            plan.SkipReason);
    }

    private decimal ResolveLegacyDriverCommission(Order order)
    {
        if (order.DriverCommissionAmount > 0m || !string.IsNullOrWhiteSpace(order.CommissionPolicySnapshot))
        {
            return order.DriverCommissionAmount;
        }

        return RoundMoney(Math.Max(order.DeliveryFee, 0m) * _settings.DriverCommissionRatePercent / 100m);
    }

    private static string BuildIdempotencyKey(Guid orderId, IReadOnlyCollection<JournalLineDraft> lines)
    {
        var payload = string.Join(
            "|",
            lines
                .OrderBy(line => line.AccountCode)
                .ThenBy(line => line.OwnerType)
                .ThenBy(line => line.OwnerId)
                .Select(line => $"{line.AccountCode}:{line.OwnerType}:{line.OwnerId}:{line.DebitAmount:0.00}:{line.CreditAmount:0.00}"));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16].ToLowerInvariant();
        return $"revenue-reconciliation:{orderId:N}:{hash}";
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record AccountKey(FinancialAccountCode AccountCode, FinancialOwnerType? OwnerType, Guid? OwnerId);

    private sealed record OrderReconciliationPlan(
        Guid OrderId,
        string OrderNumber,
        string PaymentMethod,
        decimal TotalAmount,
        bool CanApply,
        string? SkipReason,
        IReadOnlyCollection<JournalLineDraft> AdjustmentLines)
    {
        public static OrderReconciliationPlan Skipped(
            Guid orderId,
            string orderNumber,
            string paymentMethod,
            decimal totalAmount,
            string reason) =>
            new(orderId, orderNumber, paymentMethod, totalAmount, false, reason, []);
    }
}

public sealed record RevenueReconciliationPreviewDto(
    int OrdersChecked,
    int AffectedOrders,
    IReadOnlyDictionary<string, decimal> TotalsByAccount,
    IReadOnlyList<RevenueReconciliationOrderDeltaDto> Orders);

public sealed record RevenueReconciliationApplyResultDto(
    int OrdersChecked,
    int AffectedOrders,
    int AdjustmentsPosted,
    IReadOnlyList<Guid> JournalEntryIds,
    IReadOnlyDictionary<string, decimal> TotalsByAccount,
    IReadOnlyList<RevenueReconciliationOrderDeltaDto> Orders);

public sealed record RevenueReconciliationOrderDeltaDto(
    Guid OrderId,
    string OrderNumber,
    string PaymentMethod,
    decimal TotalAmount,
    IReadOnlyDictionary<string, decimal> AccountDeltas,
    bool CanApply,
    string? SkipReason);

public sealed record RevenueReconciliationApplyRequest(int? MaxOrders);
