namespace Zadana.Application.Common.Settings;

public sealed class CachingSettings
{
    public const string SectionName = "Caching";

    public RedisCachingSettings Redis { get; set; } = new();
    public CacheDurationSettings Durations { get; set; } = new();
    public int MaximumPayloadBytes { get; set; } = 256 * 1024;
    public int MaximumKeyLength { get; set; } = 1024;
}

public sealed class RedisCachingSettings
{
    public bool RequireInProduction { get; set; } = true;
    public string? ConnectionString { get; set; }
    public string InstanceName { get; set; } = "zadana";
}

public sealed class CacheDurationSettings
{
    public int GeographySeconds { get; set; } = 86400;
    public int PublicCatalogMetadataSeconds { get; set; } = 1800;
    public int BrowseBaseSeconds { get; set; } = 120;
    public int HomePublicSeconds { get; set; } = 120;
    public int FavoriteSetSeconds { get; set; } = 60;
    public int PurchaseProfileSeconds { get; set; } = 300;
    public int AdminDashboardSeconds { get; set; } = 30;

    public TimeSpan Geography => TimeSpan.FromSeconds(GeographySeconds);
    public TimeSpan PublicCatalogMetadata => TimeSpan.FromSeconds(PublicCatalogMetadataSeconds);
    public TimeSpan BrowseBase => TimeSpan.FromSeconds(BrowseBaseSeconds);
    public TimeSpan HomePublic => TimeSpan.FromSeconds(HomePublicSeconds);
    public TimeSpan FavoriteSet => TimeSpan.FromSeconds(FavoriteSetSeconds);
    public TimeSpan PurchaseProfile => TimeSpan.FromSeconds(PurchaseProfileSeconds);
    public TimeSpan AdminDashboard => TimeSpan.FromSeconds(AdminDashboardSeconds);
}
