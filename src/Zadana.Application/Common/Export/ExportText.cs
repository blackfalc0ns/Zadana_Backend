namespace Zadana.Application.Common.Export;

/// <summary>
/// Bilingual (EN / AR) labels for export files.
/// </summary>
public static class ExportText
{
    public static string Label(string english, string arabic)
    {
        english = english?.Trim() ?? string.Empty;
        arabic = arabic?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(arabic))
        {
            return english;
        }

        if (string.IsNullOrWhiteSpace(english))
        {
            return arabic;
        }

        if (string.Equals(english, arabic, StringComparison.Ordinal))
        {
            return english;
        }

        return $"{english} / {arabic}";
    }

    public static string Value(string? english, string? arabic) =>
        Label(english ?? string.Empty, arabic ?? string.Empty);

    public static ExportColumn Column(string english, string arabic, string key, double? width = null) =>
        new(Label(english, arabic), key, width);

    public static ExportKeyValue Field(string english, string arabic, string? value) =>
        new(Label(english, arabic), value ?? string.Empty);
}
