namespace Zadana.Application.Common.Settings;

public sealed class DatabasePerformanceSettings
{
    public const string SectionName = "DatabasePerformance";

    public bool LogSlowQueries { get; set; } = true;
    public int SlowQueryThresholdMilliseconds { get; set; } = 750;
    public int MaxLoggedCommandTextLength { get; set; } = 800;
}
