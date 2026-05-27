namespace Zadana.Infrastructure.Settings;

public sealed class OneSignalSettings
{
    public const string SectionName = "OneSignal";

    public bool Enabled { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string RestApiKey { get; set; } = string.Empty;
    public string DriverAppId { get; set; } = string.Empty;
    public string DriverRestApiKey { get; set; } = string.Empty;
    public string AdminWebAppId { get; set; } = string.Empty;
    public string AdminWebRestApiKey { get; set; } = string.Empty;
    public string AdminDefaultWebUrl { get; set; } = string.Empty;
    public bool UseEnvironmentVariableFallback { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.onesignal.com";
    public string DefaultWebUrl { get; set; } = string.Empty;
    public string MobileHeadsUpAndroidChannelId { get; set; } = "zadana_heads_up_notifications";
    public string MobileHeadsUpExistingAndroidChannelId { get; set; } = "zadana_heads_up_notifications";
    public int MobileHeadsUpPriority { get; set; } = 10;
    public string MobileStandardAndroidChannelId { get; set; } = "zadana_driver_general_notifications";
    public string MobileStandardExistingAndroidChannelId { get; set; } = "zadana_driver_general_notifications";
    public int MobileStandardPriority { get; set; } = 10;
    public string OrderUpdatesAndroidChannelId { get; set; } = "zadana_order_updates_realtime_v2";
    public string OrderUpdatesExistingAndroidChannelId { get; set; } = "zadana_order_updates_realtime_v2";
    public int? OrderUpdatesPriority { get; set; } = 10;
}
