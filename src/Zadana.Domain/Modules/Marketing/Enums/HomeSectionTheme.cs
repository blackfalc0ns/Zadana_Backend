namespace Zadana.Domain.Modules.Marketing.Enums;

public enum HomeSectionTheme
{
    SoftBlue = 1,
    FreshOrange = 2,
    BoldDark = 3
}

public static class HomeSectionThemeCatalog
{
    public const string SoftBlueKey = "soft-blue";
    public const string FreshOrangeKey = "fresh-orange";
    public const string BoldDarkKey = "bold-dark";

    public static IReadOnlyList<HomeSectionTheme> All { get; } =
    [
        HomeSectionTheme.SoftBlue,
        HomeSectionTheme.FreshOrange,
        HomeSectionTheme.BoldDark
    ];

    public static bool IsValidKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && TryParseKey(value, out _);

    public static bool TryParseKey(string? value, out HomeSectionTheme theme)
    {
        theme = HomeSectionTheme.SoftBlue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Equals(SoftBlueKey, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(nameof(HomeSectionTheme.SoftBlue), StringComparison.OrdinalIgnoreCase))
        {
            theme = HomeSectionTheme.SoftBlue;
            return true;
        }

        if (normalized.Equals(FreshOrangeKey, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(nameof(HomeSectionTheme.FreshOrange), StringComparison.OrdinalIgnoreCase))
        {
            theme = HomeSectionTheme.FreshOrange;
            return true;
        }

        if (normalized.Equals(BoldDarkKey, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(nameof(HomeSectionTheme.BoldDark), StringComparison.OrdinalIgnoreCase))
        {
            theme = HomeSectionTheme.BoldDark;
            return true;
        }

        return false;
    }

    public static HomeSectionTheme ParseOrDefault(string? value, HomeSectionTheme fallback = HomeSectionTheme.SoftBlue) =>
        TryParseKey(value, out var parsed) ? parsed : fallback;

    public static string ToKey(this HomeSectionTheme theme) =>
        theme switch
        {
            HomeSectionTheme.SoftBlue => SoftBlueKey,
            HomeSectionTheme.FreshOrange => FreshOrangeKey,
            HomeSectionTheme.BoldDark => BoldDarkKey,
            _ => SoftBlueKey
        };

    public static string ToClientToken(this HomeSectionTheme theme) =>
        theme switch
        {
            HomeSectionTheme.SoftBlue => "theme1",
            HomeSectionTheme.FreshOrange => "theme2",
            HomeSectionTheme.BoldDark => "theme3",
            _ => "theme1"
        };

    public static string ToArabicLabel(this HomeSectionTheme theme) =>
        theme switch
        {
            HomeSectionTheme.SoftBlue => "سمة 1",
            HomeSectionTheme.FreshOrange => "سمة 2",
            HomeSectionTheme.BoldDark => "سمة 3",
            _ => "سمة 1"
        };

    public static string ToEnglishLabel(this HomeSectionTheme theme) =>
        theme switch
        {
            HomeSectionTheme.SoftBlue => "Theme 1",
            HomeSectionTheme.FreshOrange => "Theme 2",
            HomeSectionTheme.BoldDark => "Theme 3",
            _ => "Theme 1"
        };
}
