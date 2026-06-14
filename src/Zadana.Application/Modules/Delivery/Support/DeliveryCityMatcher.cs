namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryCityMatcher
{
    public static bool Matches(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);

        return !string.IsNullOrWhiteSpace(normalizedLeft)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    public static string? Normalize(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        var normalized = city.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        return normalized switch
        {
            "dammam" => "dammam",
            "khobar" => "khobar",
            "alkhobar" => "khobar",
            "dhahran" => "dhahran",
            "jubail" or "jubel" => "jubail",
            "qatif" or "alqatif" => "qatif",
            "ahsa" or "alahsa" or "alhasa" or "hofuf" or "alhofuf" => "ahsa",
            "hafr" or "hafralbatn" or "hafralbatin" or "hafrbatin" => "hafralbatin",
            "rastanura" or "rastanorah" => "rastanura",
            "khafji" or "alkhafji" => "khafji",
            "buqayq" or "abqaiq" => "abqaiq",
            "nairyah" or "nuayriyah" => "nairyah",
            "saihat" or "sayhat" => "saihat",
            "tarut" or "tarout" => "tarut",
            "safwa" => "safwa",
            "awamiyah" => "awamiyah",
            "rahima" or "rahimah" => "rahima",
            "riyadh" => "riyadh",
            "jeddah" => "jeddah",
            _ => StripLeadingAl(normalized)
        };
    }

    private static string StripLeadingAl(string normalized) =>
        normalized.StartsWith("al", StringComparison.Ordinal) && normalized.Length > 2
            ? normalized[2..]
            : normalized;
}
