using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Finances.Enums;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class FinanceJournalMetricsService(IApplicationDbContext dbContext)
{
    public async Task<FinanceJournalPeriodMetrics> GetPeriodMetricsAsync(
        DateTime startInclusive,
        DateTime endExclusive,
        CancellationToken cancellationToken = default)
    {
        var lines = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= startInclusive &&
                line.JournalEntry.PostedAtUtc < endExclusive)
            .Where(line =>
                line.AccountCode == FinancialAccountCode.CustomerAdvance ||
                line.AccountCode == FinancialAccountCode.DriverCodReceivable ||
                line.AccountCode == FinancialAccountCode.PlatformRevenue ||
                line.AccountCode == FinancialAccountCode.TaxPayable ||
                line.AccountCode == FinancialAccountCode.DriverPayable ||
                line.AccountCode == FinancialAccountCode.RefundExpense ||
                line.AccountCode == FinancialAccountCode.GatewayFeeExpense ||
                line.AccountCode == FinancialAccountCode.SettlementVariance)
            .Select(line => new
            {
                line.AccountCode,
                line.DebitAmount,
                line.CreditAmount
            })
            .ToListAsync(cancellationToken);

        var grossCollections = lines
            .Where(line => line.AccountCode == FinancialAccountCode.CustomerAdvance)
            .Sum(line => line.CreditAmount)
            + lines
                .Where(line => line.AccountCode == FinancialAccountCode.DriverCodReceivable)
                .Sum(line => line.DebitAmount);

        var platformNetRevenue = lines
            .Where(line => line.AccountCode == FinancialAccountCode.PlatformRevenue)
            .Sum(line => line.CreditAmount);

        var vatCollected = lines
            .Where(line => line.AccountCode == FinancialAccountCode.TaxPayable)
            .Sum(line => line.CreditAmount);

        var driverPayouts = lines
            .Where(line => line.AccountCode == FinancialAccountCode.DriverPayable)
            .Sum(line => line.DebitAmount);

        var refundExposure = lines
            .Where(line => line.AccountCode == FinancialAccountCode.RefundExpense)
            .Sum(line => line.DebitAmount);

        var gatewayFees = lines
            .Where(line => line.AccountCode == FinancialAccountCode.GatewayFeeExpense)
            .Sum(line => line.DebitAmount);

        var settlementVariance = lines
            .Where(line => line.AccountCode == FinancialAccountCode.SettlementVariance)
            .Sum(line => line.DebitAmount - line.CreditAmount);

        return new FinanceJournalPeriodMetrics(
            Math.Round(grossCollections, 2),
            Math.Round(platformNetRevenue, 2),
            Math.Round(vatCollected, 2),
            Math.Round(driverPayouts, 2),
            Math.Round(refundExposure, 2),
            Math.Round(gatewayFees, 2),
            Math.Round(settlementVariance, 2));
    }

    public async Task<List<AdminChartDataPointDto>> BuildCollectionTrendAsync(
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var monthly = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= startMonth &&
                (line.AccountCode == FinancialAccountCode.CustomerAdvance ||
                 line.AccountCode == FinancialAccountCode.DriverCodReceivable ||
                 line.AccountCode == FinancialAccountCode.PlatformRevenue))
            .GroupBy(line => new
            {
                line.JournalEntry.PostedAtUtc.Year,
                line.JournalEntry.PostedAtUtc.Month
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Gross = group
                    .Where(line => line.AccountCode == FinancialAccountCode.CustomerAdvance)
                    .Sum(line => line.CreditAmount)
                    + group
                        .Where(line => line.AccountCode == FinancialAccountCode.DriverCodReceivable)
                        .Sum(line => line.DebitAmount),
                PlatformRevenue = group
                    .Where(line => line.AccountCode == FinancialAccountCode.PlatformRevenue)
                    .Sum(line => line.CreditAmount)
            })
            .ToListAsync(cancellationToken);

        var buckets = monthly.ToDictionary(
            item => (item.Year, item.Month),
            item => (item.Gross, item.PlatformRevenue));

        var points = new List<AdminChartDataPointDto>();
        for (var index = 0; index < 6; index++)
        {
            var month = startMonth.AddMonths(index);
            buckets.TryGetValue((month.Year, month.Month), out var bucket);
            points.Add(new AdminChartDataPointDto
            {
                Label = $"{month.Year}-{month.Month:D2}",
                Value = Math.Round(bucket.Item1, 2),
                SecondaryValue = Math.Round(bucket.Item2, 2)
            });
        }

        return points;
    }
}

public sealed record FinanceJournalPeriodMetrics(
    decimal GrossCollections,
    decimal PlatformNetRevenue,
    decimal VatCollected,
    decimal DriverPayouts,
    decimal RefundExposure,
    decimal GatewayFees,
    decimal SettlementVariance);
