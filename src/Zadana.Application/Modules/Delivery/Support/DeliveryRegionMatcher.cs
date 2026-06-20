namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryRegionMatcher
{
    public static bool Matches(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);

        return !string.IsNullOrWhiteSpace(normalizedLeft)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    public static string? Normalize(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return null;
        }

        var normalized = region.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace("\u0623", "\u0627")
            .Replace("\u0625", "\u0627")
            .Replace("\u0622", "\u0627")
            .Replace("\u0649", "\u064a")
            .Replace("\u0629", "\u0647");

        return normalized switch
        {
            "eastern" or "easternregion" or "easternprovince" or "المنطقةالشرقية" or "الشرقية" or "شرقية" => "eastern",
            "riyadh" or "central" or "centralregion" or "المنطقةالوسطى" or "الرياض" or "وسطى" => "riyadh",
            "makkah" or "makkahregion" or "mecca" or "western" or "westernregion" or "المنطقةالغربية" or "مكة" or "مكه" or "الغربية" => "makkah",
            "madinah" or "medina" or "almadinah" or "المدينة" or "المدينةالمنورة" or "المنطقةالمدينة" => "madinah",
            "asir" or "عسير" or "منطقةعسير" => "asir",
            "tabuk" or "تبوك" or "منطقةتبوك" => "tabuk",
            "hail" or "حail" or "حائل" or "منطقةحائل" => "hail",
            "northernborders" or "northernborder" or "الحدودالشمالية" => "northernborders",
            "jazan" or "jizan" or "جازان" or "منطقةجازان" => "jazan",
            "najran" or "نجران" or "منطقةنجران" => "najran",
            "bahah" or "albaha" or "الباحة" or "منطقةالباحة" => "bahah",
            "jawf" or "aljawf" or "الجوف" or "منطقةالجوف" => "jawf",
            "qassim" or "alqassim" or "القصيم" or "منطقةالقصيم" => "qassim",
            _ => InferRegion(normalized)
        };
    }

    private static string InferRegion(string normalized)
    {
        if (normalized.Contains("شرق", StringComparison.Ordinal) || normalized.Contains("eastern", StringComparison.Ordinal))
        {
            return "eastern";
        }

        if (normalized.Contains("رياض", StringComparison.Ordinal) || normalized.Contains("riyadh", StringComparison.Ordinal) || normalized.Contains("central", StringComparison.Ordinal))
        {
            return "riyadh";
        }

        if (normalized.Contains("مكة", StringComparison.Ordinal) || normalized.Contains("مكه", StringComparison.Ordinal) || normalized.Contains("makkah", StringComparison.Ordinal) || normalized.Contains("western", StringComparison.Ordinal))
        {
            return "makkah";
        }

        if (normalized.Contains("مدين", StringComparison.Ordinal) || normalized.Contains("madinah", StringComparison.Ordinal) || normalized.Contains("medina", StringComparison.Ordinal))
        {
            return "madinah";
        }

        return StripLeadingAl(normalized);
    }

    private static string StripLeadingAl(string normalized) =>
        normalized.StartsWith("al", StringComparison.Ordinal) && normalized.Length > 2
            ? normalized[2..]
            : normalized;
}
