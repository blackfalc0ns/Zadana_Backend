using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Support;

public static class AdminCustomerStatsCalculator
{
    public sealed record CustomerStatsRow(
        Guid Id,
        AccountStatus AccountStatus,
        bool IsLoginLocked,
        DateTime CreatedAtUtc,
        DateTime? LastLoginAtUtc,
        DateTime? LastOrderAtUtc,
        int TotalOrders,
        decimal TotalSpent,
        int RefundedOrdersCount);

    public static AdminCustomerStatsDto Calculate(IEnumerable<CustomerStatsRow> rows)
    {
        var list = rows.ToList();
        var activeCustomers = 0;
        var newCustomers = 0;
        var highRiskCustomers = 0;
        var complaintCustomers = 0;
        var repeatRefundCustomers = 0;

        foreach (var row in list)
        {
            var risk = DeriveRisk(row);
            var segment = DeriveSegment(row);
            var status = DeriveStatus(row);

            if (status == "active")
            {
                activeCustomers++;
            }

            if (segment == "new")
            {
                newCustomers++;
            }

            if (risk is "high" or "critical")
            {
                highRiskCustomers++;
            }

            if (row.RefundedOrdersCount > 0)
            {
                complaintCustomers++;
            }

            if (row.RefundedOrdersCount >= 3)
            {
                repeatRefundCustomers++;
            }
        }

        return new AdminCustomerStatsDto(
            list.Count,
            activeCustomers,
            newCustomers,
            highRiskCustomers,
            complaintCustomers,
            repeatRefundCustomers);
    }

    private static string DeriveSegment(CustomerStatsRow row)
    {
        if (row.TotalSpent >= 20_000m)
        {
            return "vip";
        }

        if (row.TotalOrders >= 25)
        {
            return "business";
        }

        if (GetAgeInDays(row.CreatedAtUtc) <= 30)
        {
            return "new";
        }

        if (row.IsLoginLocked || row.RefundedOrdersCount >= 4)
        {
            return "watchlist";
        }

        if (ComputeActiveDays30(row.LastLoginAtUtc ?? row.LastOrderAtUtc) == 0)
        {
            return "dormant";
        }

        return "new";
    }

    private static string DeriveStatus(CustomerStatsRow row)
    {
        if (row.AccountStatus is AccountStatus.Suspended or AccountStatus.Banned || row.IsLoginLocked)
        {
            return "restricted";
        }

        var activeDays = ComputeActiveDays30(row.LastLoginAtUtc ?? row.LastOrderAtUtc);
        if (activeDays == 0)
        {
            return "dormant";
        }

        if (activeDays <= 2)
        {
            return "low_activity";
        }

        return "active";
    }

    private static string DeriveRisk(CustomerStatsRow row)
    {
        if (row.IsLoginLocked || row.RefundedOrdersCount >= 5)
        {
            return "critical";
        }

        if (row.AccountStatus == AccountStatus.Suspended || row.RefundedOrdersCount >= 3)
        {
            return "high";
        }

        if (row.RefundedOrdersCount > 0 || row.TotalOrders == 0)
        {
            return "medium";
        }

        return "low";
    }

    private static int GetAgeInDays(DateTime createdAtUtc)
    {
        return Math.Max(0, (DateTime.UtcNow.Date - createdAtUtc.Date).Days);
    }

    private static int ComputeActiveDays30(DateTime? lastActivityUtc)
    {
        if (!lastActivityUtc.HasValue)
        {
            return 0;
        }

        var days = (DateTime.UtcNow.Date - lastActivityUtc.Value.Date).Days;
        return days <= 30 ? Math.Max(1, 30 - days) : 0;
    }

}
