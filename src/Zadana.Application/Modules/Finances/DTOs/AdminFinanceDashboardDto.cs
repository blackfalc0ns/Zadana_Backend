using System;
using System.Collections.Generic;

namespace Zadana.Application.Modules.Finances.DTOs;

public class AdminFinanceDashboardDto
{
    public string Period { get; set; } = null!;
    public AdminFinanceKpiDto GrossCollections { get; set; } = null!;
    public AdminFinanceKpiDto PlatformNetRevenue { get; set; } = null!;
    public AdminFinanceKpiDto CommissionRevenue { get; set; } = null!;
    public AdminFinanceKpiDto DeliveryRevenue { get; set; } = null!;
    public AdminFinanceKpiDto CodFeesCollected { get; set; } = null!;
    public AdminFinanceKpiDto VatCollected { get; set; } = null!;
    public AdminFinanceKpiDto DriverPayouts { get; set; } = null!;
    public AdminFinanceKpiDto RefundExposure { get; set; } = null!;
    public List<AdminRevenueCompositionSegmentDto> RevenueComposition { get; set; } = [];
    public List<AdminChartDataPointDto> CollectionTrend { get; set; } = [];
    public List<AdminChartDataPointDto> RevenueTrend { get; set; } = [];
    public List<AdminFinanceDashboardAlertDto> Alerts { get; set; } = [];
}

public class AdminFinanceKpiDto
{
    public string Id { get; set; } = null!;
    public string LabelKey { get; set; } = null!;
    public decimal Value { get; set; }
    public string FormattedValue { get; set; } = null!;
    public string? Currency { get; set; }
    public string Trend { get; set; } = null!; // "up", "down", "flat"
    public decimal TrendPercent { get; set; }
    public string? TrendLabel { get; set; }
    public string? Severity { get; set; } // "success", "warning", "danger", "neutral"
    public string? ClickRoute { get; set; }
    public string Icon { get; set; } = null!;
    public List<decimal>? Sparkline { get; set; }
}

public class AdminRevenueCompositionSegmentDto
{
    public string Id { get; set; } = null!;
    public string LabelKey { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal Percent { get; set; }
    public string Color { get; set; } = null!;
}

public class AdminChartDataPointDto
{
    public string Label { get; set; } = null!;
    public decimal Value { get; set; }
    public decimal? SecondaryValue { get; set; }
}

public class AdminFinanceDashboardAlertDto
{
    public string Id { get; set; } = null!;
    public string Severity { get; set; } = null!; // "critical", "warning", "info"
    public string TitleKey { get; set; } = null!;
    public string DescriptionKey { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? OrderId { get; set; }
    public string? EntityName { get; set; }
    public decimal? Amount { get; set; }
    public string? ActionKey { get; set; }
    public string? ActionRoute { get; set; }
    public string Timestamp { get; set; } = null!;
}
