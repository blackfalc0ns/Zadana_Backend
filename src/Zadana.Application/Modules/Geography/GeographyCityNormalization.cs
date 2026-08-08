namespace Zadana.Application.Modules.Geography;

public static class GeographyCityNormalization
{
    public static string? NormalizeCityName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace("أ", "ا")
            .Replace("إ", "ا")
            .Replace("آ", "ا")
            .Replace("ى", "ي")
            .Replace("ة", "ه");

        return GeographyCityAliases.MapAliasKey(normalized);
    }
}
