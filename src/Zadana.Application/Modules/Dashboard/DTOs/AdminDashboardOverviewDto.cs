namespace Zadana.Application.Modules.Dashboard.DTOs;

public sealed class AdminDashboardOverviewDto
{
    public AdminDashboardMetaDto Meta { get; init; } = new();
    public AdminDashboardFilterOptionsDto Filters { get; init; } = new();
    public IReadOnlyList<AdminDashboardKpiDto> HeroKpis { get; init; } = [];
    public AdminDashboardChartBundleDto Charts { get; init; } = new();
    public IReadOnlyList<AdminDashboardAlertDto> Alerts { get; init; } = [];
    public AdminDashboardQueueBundleDto Queues { get; init; } = new();
    public IReadOnlyList<AdminDashboardAttentionItemDto> AttentionItems { get; init; } = [];
    public IReadOnlyList<AdminDashboardAuditItemDto> AuditFeed { get; init; } = [];
    public AdminDashboardSectionBundleDto Sections { get; init; } = new();
}

public sealed class AdminDashboardMetaDto
{
    public string Period { get; init; } = "today";
    public string Region { get; init; } = "all";
    public Guid? VendorId { get; init; }
    public string ScopeSummary { get; init; } = string.Empty;
    public string Mode { get; init; } = "live";
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class AdminDashboardFilterOptionsDto
{
    public IReadOnlyList<AdminDashboardFilterOptionDto> DateRanges { get; init; } = [];
    public IReadOnlyList<AdminDashboardFilterOptionDto> Regions { get; init; } = [];
    public IReadOnlyList<AdminDashboardFilterOptionDto> Vendors { get; init; } = [];
}

public sealed class AdminDashboardFilterOptionDto
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int? Count { get; init; }
}

public sealed class AdminDashboardKpiDto
{
    public string Id { get; init; } = string.Empty;
    public string LabelKey { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string DisplayValue { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public string ChangeLabel { get; init; } = string.Empty;
    public string TrendDirection { get; init; } = "flat";
    public string Severity { get; init; } = "neutral";
    public string ContextKey { get; init; } = string.Empty;
}

public sealed class AdminDashboardChartBundleDto
{
    public AdminDashboardSeriesChartDto OrdersTrend { get; init; } = new();
    public AdminDashboardSeriesChartDto RevenueTrend { get; init; } = new();
    public IReadOnlyList<AdminDashboardRegionPressureDto> RegionPressure { get; init; } = [];
    public IReadOnlyList<AdminDashboardDistributionBucketDto> VendorReadiness { get; init; } = [];
    public IReadOnlyList<AdminDashboardDistributionBucketDto> DriverReadiness { get; init; } = [];
}

public sealed class AdminDashboardSeriesChartDto
{
    public string TitleKey { get; init; } = string.Empty;
    public string DescriptionKey { get; init; } = string.Empty;
    public IReadOnlyList<AdminDashboardChartSeriesDto> Series { get; init; } = [];
}

public sealed class AdminDashboardChartSeriesDto
{
    public string Id { get; init; } = string.Empty;
    public string LabelKey { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public IReadOnlyList<AdminDashboardChartPointDto> Points { get; init; } = [];
}

public sealed class AdminDashboardChartPointDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Value { get; init; }
}

public sealed class AdminDashboardRegionPressureDto
{
    public string RegionKey { get; init; } = string.Empty;
    public string RegionLabel { get; init; } = string.Empty;
    public int LateOrders { get; init; }
    public int PaymentIssues { get; init; }
    public int DriverGap { get; init; }
    public int Score { get; init; }
    public string Route { get; init; } = "/orders";
}

public sealed class AdminDashboardDistributionBucketDto
{
    public string Id { get; init; } = string.Empty;
    public string LabelKey { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Share { get; init; }
    public string Color { get; init; } = string.Empty;
    public string Severity { get; init; } = "neutral";
}

public sealed class AdminDashboardAlertDto
{
    public string Id { get; init; } = string.Empty;
    public string Severity { get; init; } = "neutral";
    public string TitleKey { get; init; } = string.Empty;
    public string SummaryKey { get; init; } = string.Empty;
    public Dictionary<string, object?> SummaryParams { get; init; } = [];
    public int Count { get; init; }
    public string Route { get; init; } = string.Empty;
}

public sealed class AdminDashboardQueueBundleDto
{
    public IReadOnlyList<AdminDashboardQueueDto> Live { get; init; } = [];
    public IReadOnlyList<AdminDashboardQueueDto> Risk { get; init; } = [];
}

public sealed class AdminDashboardQueueDto
{
    public string Id { get; init; } = string.Empty;
    public string LabelKey { get; init; } = string.Empty;
    public int Count { get; init; }
    public string HelperKey { get; init; } = string.Empty;
    public string Severity { get; init; } = "neutral";
    public string Route { get; init; } = string.Empty;
}

public sealed class AdminDashboardAttentionItemDto
{
    public string Id { get; init; } = string.Empty;
    public string EntityLabelKey { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Priority { get; init; } = "neutral";
    public string Route { get; init; } = string.Empty;
    public string ActionLabelKey { get; init; } = string.Empty;
}

public sealed class AdminDashboardAuditItemDto
{
    public string Id { get; init; } = string.Empty;
    public string TitleKey { get; init; } = string.Empty;
    public Dictionary<string, object?> TitleParams { get; init; } = [];
    public string SubtitleKey { get; init; } = string.Empty;
    public Dictionary<string, object?> SubtitleParams { get; init; } = [];
    public string Severity { get; init; } = "neutral";
    public DateTime TimestampUtc { get; init; }
    public string Route { get; init; } = string.Empty;
}

public sealed class AdminDashboardSectionBundleDto
{
    public AdminDashboardSectionDto SystemHealth { get; init; } = new();
    public AdminDashboardSectionDto OrderOps { get; init; } = new();
    public AdminDashboardSectionDto VendorOps { get; init; } = new();
    public AdminDashboardSectionDto DriverOps { get; init; } = new();
    public AdminDashboardSectionDto CustomerSupport { get; init; } = new();
    public AdminDashboardSectionDto FinanceOps { get; init; } = new();
    public AdminDashboardSectionDto CatalogHealth { get; init; } = new();
    public AdminDashboardSectionDto MarketingPulse { get; init; } = new();
    public AdminDashboardSectionDto AccessSecurity { get; init; } = new();
}

public sealed class AdminDashboardSectionDto
{
    public string Id { get; init; } = string.Empty;
    public string TitleKey { get; init; } = string.Empty;
    public string DescriptionKey { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
    public AdminDashboardSectionStatusDto Status { get; init; } = new();
    public IReadOnlyList<AdminDashboardStatCardDto> Stats { get; init; } = [];
    public IReadOnlyList<AdminDashboardRankedListDto> RankedLists { get; init; } = [];
    public IReadOnlyList<AdminDashboardExceptionRowDto> Exceptions { get; init; } = [];
}

public sealed class AdminDashboardSectionStatusDto
{
    public string Severity { get; init; } = "neutral";
    public string SummaryKey { get; init; } = string.Empty;
    public Dictionary<string, object?> SummaryParams { get; init; } = [];
}

public sealed class AdminDashboardStatCardDto
{
    public string Id { get; init; } = string.Empty;
    public string LabelKey { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string DisplayValue { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public string Tone { get; init; } = "neutral";
    public string HelperKey { get; init; } = string.Empty;
}

public sealed class AdminDashboardRankedListDto
{
    public string Id { get; init; } = string.Empty;
    public string TitleKey { get; init; } = string.Empty;
    public string DescriptionKey { get; init; } = string.Empty;
    public IReadOnlyList<AdminDashboardRankedRowDto> Rows { get; init; } = [];
}

public sealed class AdminDashboardRankedRowDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? SecondaryValue { get; init; }
    public string? MetaLabel { get; init; }
    public string Severity { get; init; } = "neutral";
    public string Route { get; init; } = string.Empty;
}

public sealed class AdminDashboardExceptionRowDto
{
    public string Id { get; init; } = string.Empty;
    public string EntityLabel { get; init; } = string.Empty;
    public string IssueLabel { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = string.Empty;
    public string MetricLabel { get; init; } = string.Empty;
    public string Severity { get; init; } = "neutral";
    public string Route { get; init; } = string.Empty;
}
