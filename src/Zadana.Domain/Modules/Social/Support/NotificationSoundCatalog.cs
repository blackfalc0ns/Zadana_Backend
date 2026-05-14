namespace Zadana.Domain.Modules.Social.Support;

public static class NotificationSoundCatalog
{
    public const string Classic = "classic";
    public const string Chime = "chime";
    public const string Soft = "soft";
    public const string Urgent = "urgent";
    public const string Off = "off";

    public static string Normalize(string? value, string fallback = Classic)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            Chime => Chime,
            Soft => Soft,
            Urgent => Urgent,
            Off => Off,
            Classic => Classic,
            _ => fallback
        };
    }
}
