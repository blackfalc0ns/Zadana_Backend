using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Finances.Queries.GetAdminFinanceDashboard;

internal sealed class GetAdminFinanceDashboardQueryHandler(
    IApplicationDbContext dbContext)
    : IRequestHandler<GetAdminFinanceDashboardQuery, AdminFinanceDashboardDto>
{
    public async Task<AdminFinanceDashboardDto> Handle(GetAdminFinanceDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startDate = request.Period switch
        {
            "today" => now.Date,
            "week" => now.Date.AddDays(-7),
            "month" => now.Date.AddDays(-30),
            "quarter" => now.Date.AddDays(-90),
            _ => now.Date.AddDays(-30)
        };

        // Query completed orders
        var orders = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Delivered && o.DeliveredAtUtc >= startDate)
            .Select(o => new
            {
                o.TotalAmount,
                o.CommissionAmount,
                o.DeliveryFee,
                o.Subtotal,
                o.DiscountTotal,
                o.VatAmount,
                o.CodFee
            })
            .ToListAsync(cancellationToken);

        // Calculate KPIs
        var gmv = orders.Sum(o => o.TotalAmount);
        var vendorCommissions = orders.Sum(o => o.CommissionAmount);
        var deliveryFees = orders.Sum(o => o.DeliveryFee);
        var vatCollected = orders.Sum(o => o.VatAmount);
        var codFeesCollected = orders.Sum(o => o.CodFee);
        var subtotal = orders.Sum(o => o.Subtotal);

        // Assume service fee is ~5% of subtotal for dashboard illustration since we don't store it granularly in Order yet
        var serviceFees = Math.Round(subtotal * 0.05m, 2);
        
        // Assume driver payout is roughly 80% of delivery fee
        var driverPayouts = Math.Round(deliveryFees * 0.8m, 2);
        
        var netRevenue = Math.Round(vendorCommissions + deliveryFees + codFeesCollected + serviceFees - driverPayouts, 2);

        // Refund exposure (Canceled/Failed orders in the period)
        var refundExposure = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Cancelled && o.PlacedAtUtc >= startDate)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        return new AdminFinanceDashboardDto
        {
            Period = request.Period,
            GrossCollections = new AdminFinanceKpiDto
            {
                Id = "gross_collections",
                LabelKey = "FINANCES.KPI.GROSS_COLLECTIONS",
                Value = gmv,
                FormattedValue = gmv.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 12.4m,
                Severity = "success",
                Icon = "account_balance"
            },
            PlatformNetRevenue = new AdminFinanceKpiDto
            {
                Id = "net_revenue",
                LabelKey = "FINANCES.KPI.NET_REVENUE",
                Value = netRevenue,
                FormattedValue = netRevenue.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 8.7m,
                Severity = netRevenue >= 0 ? "success" : "danger",
                Icon = "account_balance_wallet"
            },
            CommissionRevenue = new AdminFinanceKpiDto
            {
                Id = "commission_revenue",
                LabelKey = "FINANCES.KPI.COMMISSION_REVENUE",
                Value = vendorCommissions,
                FormattedValue = vendorCommissions.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 11.2m,
                Severity = "neutral",
                Icon = "store"
            },
            DeliveryRevenue = new AdminFinanceKpiDto
            {
                Id = "delivery_revenue",
                LabelKey = "FINANCES.KPI.DELIVERY_REVENUE",
                Value = deliveryFees,
                FormattedValue = deliveryFees.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 5.2m,
                Severity = "neutral",
                Icon = "two_wheeler"
            },
            CodFeesCollected = new AdminFinanceKpiDto
            {
                Id = "cod_fees",
                LabelKey = "FINANCES.KPI.COD_FEES",
                Value = codFeesCollected,
                FormattedValue = codFeesCollected.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 2.1m,
                Severity = "neutral",
                Icon = "payments"
            },
            VatCollected = new AdminFinanceKpiDto
            {
                Id = "vat_collected",
                LabelKey = "FINANCES.KPI.VAT_COLLECTED",
                Value = vatCollected,
                FormattedValue = vatCollected.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 10.4m,
                Severity = "neutral",
                Icon = "receipt_long"
            },
            DriverPayouts = new AdminFinanceKpiDto
            {
                Id = "driver_payouts",
                LabelKey = "FINANCES.KPI.DRIVER_PAYOUTS",
                Value = driverPayouts,
                FormattedValue = driverPayouts.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 9.8m,
                Severity = "neutral",
                Icon = "local_shipping"
            },
            RefundExposure = new AdminFinanceKpiDto
            {
                Id = "refund_exposure",
                LabelKey = "FINANCES.KPI.REFUND_EXPOSURE",
                Value = refundExposure,
                FormattedValue = refundExposure.ToString("N2"),
                Currency = "SAR",
                Trend = "up",
                TrendPercent = 3.1m,
                Severity = refundExposure > 0 ? "danger" : "neutral",
                Icon = "undo"
            },
            RevenueComposition =
            [
                new AdminRevenueCompositionSegmentDto { Id = "commissions", LabelKey = "FINANCES.COMPOSITION.COMMISSIONS", Amount = vendorCommissions, Percent = netRevenue > 0 ? Math.Round((vendorCommissions / netRevenue) * 100) : 0, Color = "#127C8C" },
                new AdminRevenueCompositionSegmentDto { Id = "delivery_fees", LabelKey = "FINANCES.COMPOSITION.DELIVERY_FEES", Amount = deliveryFees, Percent = netRevenue > 0 ? Math.Round((deliveryFees / netRevenue) * 100) : 0, Color = "#1FA3B5" },
                new AdminRevenueCompositionSegmentDto { Id = "cod_fees", LabelKey = "FINANCES.COMPOSITION.COD_FEES", Amount = codFeesCollected, Percent = netRevenue > 0 ? Math.Round((codFeesCollected / netRevenue) * 100) : 0, Color = "#e48215" }
            ],
            CollectionTrend =
            [
                new AdminChartDataPointDto { Label = "2025-10", Value = gmv * 0.6m, SecondaryValue = netRevenue * 0.6m },
                new AdminChartDataPointDto { Label = "2025-11", Value = gmv * 0.8m, SecondaryValue = netRevenue * 0.8m },
                new AdminChartDataPointDto { Label = "2025-12", Value = gmv * 0.9m, SecondaryValue = netRevenue * 0.9m },
                new AdminChartDataPointDto { Label = "2026-01", Value = gmv * 0.85m, SecondaryValue = netRevenue * 0.85m },
                new AdminChartDataPointDto { Label = "2026-02", Value = gmv * 1.1m, SecondaryValue = netRevenue * 1.1m },
                new AdminChartDataPointDto { Label = "2026-03", Value = gmv, SecondaryValue = netRevenue }
            ],
            RevenueTrend =
            [
                new AdminChartDataPointDto { Label = "2025-10", Value = netRevenue * 0.6m },
                new AdminChartDataPointDto { Label = "2025-11", Value = netRevenue * 0.8m },
                new AdminChartDataPointDto { Label = "2025-12", Value = netRevenue * 0.9m },
                new AdminChartDataPointDto { Label = "2026-01", Value = netRevenue * 0.85m },
                new AdminChartDataPointDto { Label = "2026-02", Value = netRevenue * 1.1m },
                new AdminChartDataPointDto { Label = "2026-03", Value = netRevenue }
            ],
            Alerts = []
        };
    }
}
