using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Finances.Queries.GetAdminFinanceDashboard;

internal sealed class GetAdminFinanceDashboardQueryHandler(
    IApplicationDbContext dbContext)
    : IRequestHandler<GetAdminFinanceDashboardQuery, AdminFinanceDashboardDto>
{
    private static readonly SettlementStatus[] OpenSettlementStatuses =
    [
        SettlementStatus.Pending,
        SettlementStatus.PendingReview,
        SettlementStatus.Approved,
        SettlementStatus.OnHold,
        SettlementStatus.Processing
    ];

    private static readonly SettlementStatus[] PaidSettlementStatuses =
    [
        SettlementStatus.PaidOut,
        SettlementStatus.Settled
    ];

    public async Task<AdminFinanceDashboardDto> Handle(
        GetAdminFinanceDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (currentStart, currentEnd, previousStart, previousEnd) = ResolvePeriodWindow(request.Period, now);

        var currentOrders = await LoadDeliveredOrderMetricsAsync(currentStart, currentEnd, cancellationToken);
        var previousOrders = await LoadDeliveredOrderMetricsAsync(previousStart, previousEnd, cancellationToken);

        var currentDriverPayouts = await SumDriverPayoutsAsync(currentStart, currentEnd, cancellationToken);
        var previousDriverPayouts = await SumDriverPayoutsAsync(previousStart, previousEnd, cancellationToken);

        var currentRefundExposure = await SumRefundExposureAsync(currentStart, currentEnd, cancellationToken);
        var previousRefundExposure = await SumRefundExposureAsync(previousStart, previousEnd, cancellationToken);

        var collectionTrend = await BuildCollectionTrendAsync(now, cancellationToken);
        var revenueTrend = await BuildRefundTrendAsync(now, cancellationToken);
        var alerts = await BuildAlertsAsync(cancellationToken);

        var platformNetRevenue = currentOrders.PlatformNetRevenue;
        var revenueComposition = BuildRevenueComposition(currentOrders, platformNetRevenue);

        return new AdminFinanceDashboardDto
        {
            Period = request.Period,
            GrossCollections = BuildKpi(
                "gross_collections",
                "FINANCES.KPI.GROSS_COLLECTIONS",
                currentOrders.GrossCollections,
                previousOrders.GrossCollections,
                "account_balance",
                platformNetRevenue >= 0 ? "success" : "danger",
                "/finances/ledger"),
            PlatformNetRevenue = BuildKpi(
                "platform_net_revenue",
                "FINANCES.KPI.PLATFORM_NET_REVENUE",
                platformNetRevenue,
                previousOrders.PlatformNetRevenue,
                "account_balance_wallet",
                platformNetRevenue >= 0 ? "success" : "danger",
                "/finances/overview"),
            CommissionRevenue = BuildKpi(
                "commission_revenue",
                "FINANCES.KPI.COMMISSION_REVENUE",
                currentOrders.CommissionRevenue,
                previousOrders.CommissionRevenue,
                "store",
                "neutral",
                "/finances/settlements?entityType=vendor"),
            DeliveryRevenue = BuildKpi(
                "delivery_revenue",
                "FINANCES.KPI.DELIVERY_REVENUE",
                currentOrders.DeliveryRevenue,
                previousOrders.DeliveryRevenue,
                "two_wheeler",
                "neutral",
                "/finances/overview"),
            CodFeesCollected = BuildKpi(
                "cod_fees",
                "FINANCES.KPI.COD_FEES_COLLECTED",
                currentOrders.CodFeesCollected,
                previousOrders.CodFeesCollected,
                "payments",
                "neutral",
                "/finances/cod"),
            VatCollected = BuildKpi(
                "vat_collected",
                "FINANCES.KPI.VAT_COLLECTED",
                currentOrders.VatCollected,
                previousOrders.VatCollected,
                "receipt_long",
                "neutral",
                "/finances/ledger"),
            DriverPayouts = BuildKpi(
                "driver_payouts",
                "FINANCES.KPI.DRIVER_PAYOUTS",
                currentDriverPayouts,
                previousDriverPayouts,
                "local_shipping",
                "neutral",
                "/finances/settlements?entityType=driver"),
            RefundExposure = BuildKpi(
                "refund_exposure",
                "FINANCES.KPI.REFUND_EXPOSURE",
                currentRefundExposure,
                previousRefundExposure,
                "undo",
                currentRefundExposure > 0 ? "danger" : "neutral",
                "/finances/refunds"),
            RevenueComposition = revenueComposition,
            CollectionTrend = collectionTrend,
            RevenueTrend = revenueTrend,
            Alerts = alerts
        };
    }

    private async Task<DeliveredOrderMetrics> LoadDeliveredOrderMetricsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.Delivered &&
                order.DeliveredAtUtc >= start &&
                order.DeliveredAtUtc < end)
            .Select(order => new
            {
                order.TotalAmount,
                order.CommissionAmount,
                order.VendorCommissionAmount,
                order.DriverCommissionAmount,
                order.VatAmount,
                order.CodFee
            })
            .ToListAsync(cancellationToken);

        var commissionRevenue = orders.Sum(order =>
            order.VendorCommissionAmount > 0 ? order.VendorCommissionAmount : order.CommissionAmount);
        var deliveryRevenue = orders.Sum(order => order.DriverCommissionAmount);
        var codFeesCollected = orders.Sum(order => order.CodFee);
        var vatCollected = orders.Sum(order => order.VatAmount);
        var grossCollections = orders.Sum(order => order.TotalAmount);
        var platformNetRevenue = Math.Round(commissionRevenue + deliveryRevenue + codFeesCollected, 2);

        return new DeliveredOrderMetrics(
            grossCollections,
            platformNetRevenue,
            commissionRevenue,
            deliveryRevenue,
            codFeesCollected,
            vatCollected);
    }

    private async Task<decimal> SumDriverPayoutsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var settlementPayouts = await dbContext.Settlements
            .AsNoTracking()
            .Where(settlement =>
                settlement.OwnerType == SettlementOwnerType.Driver &&
                PaidSettlementStatuses.Contains(settlement.Status) &&
                settlement.ProcessedAtUtc >= start &&
                settlement.ProcessedAtUtc < end)
            .SumAsync(settlement => (decimal?)settlement.NetAmount, cancellationToken) ?? 0m;

        if (settlementPayouts > 0)
        {
            return Math.Round(settlementPayouts, 2);
        }

        var journalPayouts = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.AccountCode == FinancialAccountCode.DriverPayable &&
                line.DebitAmount > 0 &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= start &&
                line.JournalEntry.PostedAtUtc < end)
            .SumAsync(line => (decimal?)line.DebitAmount, cancellationToken) ?? 0m;

        return Math.Round(journalPayouts, 2);
    }

    private async Task<decimal> SumRefundExposureAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var refundExpense = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.AccountCode == FinancialAccountCode.RefundExpense &&
                line.DebitAmount > 0 &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= start &&
                line.JournalEntry.PostedAtUtc < end)
            .SumAsync(line => (decimal?)line.DebitAmount, cancellationToken) ?? 0m;

        if (refundExpense > 0)
        {
            return Math.Round(refundExpense, 2);
        }

        var cancelledOrders = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.Cancelled &&
                order.CancelledAtUtc >= start &&
                order.CancelledAtUtc < end)
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

        return Math.Round(cancelledOrders, 2);
    }

    private async Task<List<AdminChartDataPointDto>> BuildCollectionTrendAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var monthly = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.Delivered &&
                order.DeliveredAtUtc >= startMonth)
            .GroupBy(order => new
            {
                order.DeliveredAtUtc!.Value.Year,
                order.DeliveredAtUtc!.Value.Month
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Gross = group.Sum(order => order.TotalAmount),
                Net = group.Sum(order =>
                    (order.VendorCommissionAmount > 0 ? order.VendorCommissionAmount : order.CommissionAmount)
                    + order.DriverCommissionAmount
                    + order.CodFee)
            })
            .ToListAsync(cancellationToken);

        return BuildMonthlySeries(startMonth, monthly.ToDictionary(
            item => (item.Year, item.Month),
            item => (item.Gross, item.Net)));
    }

    private async Task<List<AdminChartDataPointDto>> BuildRefundTrendAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var monthlyRefunds = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.AccountCode == FinancialAccountCode.RefundExpense &&
                line.DebitAmount > 0 &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= startMonth)
            .GroupBy(line => new
            {
                line.JournalEntry.PostedAtUtc.Year,
                line.JournalEntry.PostedAtUtc.Month
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Amount = group.Sum(line => line.DebitAmount)
            })
            .ToListAsync(cancellationToken);

        if (monthlyRefunds.Count == 0)
        {
            monthlyRefunds = await dbContext.Orders
                .AsNoTracking()
                .Where(order =>
                    order.Status == OrderStatus.Cancelled &&
                    order.CancelledAtUtc >= startMonth)
                .GroupBy(order => new
                {
                    order.CancelledAtUtc!.Value.Year,
                    order.CancelledAtUtc!.Value.Month
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Amount = group.Sum(order => order.TotalAmount)
                })
                .ToListAsync(cancellationToken);
        }

        return BuildMonthlySeries(
            startMonth,
            monthlyRefunds.ToDictionary(
                item => (item.Year, item.Month),
                item => (item.Amount, 0m)),
            includeSecondary: false);
    }

    private async Task<List<AdminFinanceDashboardAlertDto>> BuildAlertsAsync(CancellationToken cancellationToken)
    {
        var alerts = new List<AdminFinanceDashboardAlertDto>();
        var now = DateTime.UtcNow;

        var codDrivers = await dbContext.Wallets
            .AsNoTracking()
            .Where(wallet =>
                wallet.OwnerType == WalletOwnerType.Driver &&
                wallet.CodOwedBalance > 0)
            .OrderByDescending(wallet => wallet.CodOwedBalance)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (codDrivers.Count > 0)
        {
            var driverIds = codDrivers.Select(wallet => wallet.OwnerId).ToList();
            var drivers = await dbContext.Drivers
                .AsNoTracking()
                .Include(driver => driver.User)
                .Where(driver => driverIds.Contains(driver.Id))
                .ToDictionaryAsync(driver => driver.Id, cancellationToken);

            foreach (var wallet in codDrivers)
            {
                drivers.TryGetValue(wallet.OwnerId, out var driver);
                alerts.Add(new AdminFinanceDashboardAlertDto
                {
                    Id = $"cod-{wallet.OwnerId:N}",
                    Severity = wallet.CodOwedBalance >= 500 ? "critical" : "warning",
                    TitleKey = "FINANCES.ALERTS.COD_OVERDUE_TITLE",
                    DescriptionKey = "FINANCES.ALERTS.COD_OVERDUE_DESC",
                    EntityType = "driver",
                    EntityId = wallet.OwnerId.ToString(),
                    EntityName = driver?.User.FullName ?? "Driver",
                    Amount = wallet.CodOwedBalance,
                    ActionRoute = "/finances/cod",
                    Timestamp = now.ToString("O")
                });
            }
        }

        var openSettlements = await dbContext.Settlements
            .AsNoTracking()
            .Where(settlement => OpenSettlementStatuses.Contains(settlement.Status))
            .OrderByDescending(settlement => settlement.NetAmount)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var settlement in openSettlements)
        {
            alerts.Add(new AdminFinanceDashboardAlertDto
            {
                Id = $"settlement-{settlement.Id:N}",
                Severity = settlement.Status == SettlementStatus.Processing ? "warning" : "info",
                TitleKey = "FINANCES.ALERTS.SETTLEMENT_PENDING_TITLE",
                DescriptionKey = "FINANCES.ALERTS.SETTLEMENT_PENDING_DESC",
                EntityType = settlement.OwnerType == SettlementOwnerType.Vendor ? "vendor" : "driver",
                EntityId = settlement.OwnerId.ToString(),
                Amount = settlement.NetAmount,
                ActionRoute = "/finances/settlements",
                Timestamp = settlement.UpdatedAtUtc.ToString("O")
            });
        }

        var failedSettlements = await dbContext.Settlements
            .AsNoTracking()
            .Where(settlement =>
                settlement.Status == SettlementStatus.PayoutFailed ||
                settlement.Status == SettlementStatus.Failed)
            .OrderByDescending(settlement => settlement.UpdatedAtUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        foreach (var settlement in failedSettlements)
        {
            alerts.Add(new AdminFinanceDashboardAlertDto
            {
                Id = $"settlement-failed-{settlement.Id:N}",
                Severity = "critical",
                TitleKey = "FINANCES.ALERTS.SETTLEMENT_FAILED_TITLE",
                DescriptionKey = "FINANCES.ALERTS.SETTLEMENT_FAILED_DESC",
                EntityType = settlement.OwnerType == SettlementOwnerType.Vendor ? "vendor" : "driver",
                EntityId = settlement.OwnerId.ToString(),
                Amount = settlement.NetAmount,
                ActionRoute = "/finances/settlements",
                Timestamp = settlement.UpdatedAtUtc.ToString("O")
            });
        }

        return alerts
            .OrderByDescending(alert => alert.Severity == "critical")
            .ThenByDescending(alert => alert.Amount ?? 0m)
            .Take(10)
            .ToList();
    }

    private static List<AdminRevenueCompositionSegmentDto> BuildRevenueComposition(
        DeliveredOrderMetrics metrics,
        decimal platformNetRevenue)
    {
        var segments = new List<(string Id, string LabelKey, decimal Amount, string Color)>
        {
            ("commissions", "FINANCES.COMPOSITION.COMMISSIONS", metrics.CommissionRevenue, "#127C8C"),
            ("delivery_fees", "FINANCES.COMPOSITION.DELIVERY_FEES", metrics.DeliveryRevenue, "#1FA3B5"),
            ("cod_fees", "FINANCES.COMPOSITION.COD_FEES", metrics.CodFeesCollected, "#e48215")
        };

        if (metrics.VatCollected > 0)
        {
            segments.Add(("vat", "FINANCES.COMPOSITION.VAT", metrics.VatCollected, "#94a3b8"));
        }

        return segments
            .Where(segment => segment.Amount > 0)
            .Select(segment => new AdminRevenueCompositionSegmentDto
            {
                Id = segment.Id,
                LabelKey = segment.LabelKey,
                Amount = Math.Round(segment.Amount, 2),
                Percent = platformNetRevenue > 0
                    ? Math.Round((segment.Amount / platformNetRevenue) * 100, 1)
                    : 0,
                Color = segment.Color
            })
            .ToList();
    }

    private static List<AdminChartDataPointDto> BuildMonthlySeries(
        DateTime startMonth,
        Dictionary<(int Year, int Month), (decimal Primary, decimal Secondary)> values,
        bool includeSecondary = true)
    {
        var points = new List<AdminChartDataPointDto>();

        for (var index = 0; index < 6; index++)
        {
            var month = startMonth.AddMonths(index);
            values.TryGetValue((month.Year, month.Month), out var bucket);

            points.Add(new AdminChartDataPointDto
            {
                Label = $"{month.Year}-{month.Month:D2}",
                Value = Math.Round(bucket.Primary, 2),
                SecondaryValue = includeSecondary ? Math.Round(bucket.Secondary, 2) : null
            });
        }

        return points;
    }

    private static AdminFinanceKpiDto BuildKpi(
        string id,
        string labelKey,
        decimal currentValue,
        decimal previousValue,
        string icon,
        string severity,
        string? clickRoute)
    {
        var (trend, trendPercent) = CalculateTrend(currentValue, previousValue);

        return new AdminFinanceKpiDto
        {
            Id = id,
            LabelKey = labelKey,
            Value = Math.Round(currentValue, 2),
            FormattedValue = Math.Round(currentValue, 2).ToString("N2"),
            Currency = "SAR",
            Trend = trend,
            TrendPercent = trendPercent,
            TrendLabel = "FINANCES.KPI.VS_PREVIOUS_PERIOD",
            Severity = severity,
            Icon = icon,
            ClickRoute = clickRoute
        };
    }

    private static (string Trend, decimal TrendPercent) CalculateTrend(decimal current, decimal previous)
    {
        if (previous == 0m)
        {
            if (current > 0m)
            {
                return ("up", 100m);
            }

            return ("flat", 0m);
        }

        var percent = Math.Round(((current - previous) / previous) * 100m, 1);

        if (percent > 0m)
        {
            return ("up", percent);
        }

        if (percent < 0m)
        {
            return ("down", Math.Abs(percent));
        }

        return ("flat", 0m);
    }

    private static (DateTime Start, DateTime End, DateTime PreviousStart, DateTime PreviousEnd) ResolvePeriodWindow(
        string period,
        DateTime now)
    {
        var end = now;
        DateTime start;
        TimeSpan duration;

        switch (period)
        {
            case "today":
                start = now.Date;
                duration = TimeSpan.FromDays(1);
                break;
            case "week":
                start = now.Date.AddDays(-7);
                duration = TimeSpan.FromDays(7);
                break;
            case "quarter":
                start = now.Date.AddDays(-90);
                duration = TimeSpan.FromDays(90);
                break;
            default:
                start = now.Date.AddDays(-30);
                duration = TimeSpan.FromDays(30);
                break;
        }

        var previousEnd = start;
        var previousStart = start - duration;
        return (start, end, previousStart, previousEnd);
    }

    private sealed record DeliveredOrderMetrics(
        decimal GrossCollections,
        decimal PlatformNetRevenue,
        decimal CommissionRevenue,
        decimal DeliveryRevenue,
        decimal CodFeesCollected,
        decimal VatCollected);
}
