namespace Zadana.Infrastructure.Settings;

public sealed class OneSignalSettings
{
    public const string SectionName = "OneSignal";

    public bool Enabled { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string RestApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.onesignal.com";
    public string DefaultWebUrl { get; set; } = string.Empty;
}
