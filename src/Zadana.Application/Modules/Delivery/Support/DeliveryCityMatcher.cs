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
            .Replace("_", string.Empty)
            .Replace("\u0623", "\u0627")
            .Replace("\u0625", "\u0627")
            .Replace("\u0622", "\u0627")
            .Replace("\u0649", "\u064a")
            .Replace("\u0629", "\u0647");

        return normalized switch
        {
            "dammam" or "\u0627\u0644\u062f\u0645\u0627\u0645" or "\u062f\u0645\u0627\u0645" => "dammam",
            "khobar" or "alkhobar" or "\u0627\u0644\u062e\u0628\u0631" or "\u062e\u0628\u0631" => "khobar",
            "dhahran" or "\u0627\u0644\u0638\u0647\u0631\u0627\u0646" or "\u0638\u0647\u0631\u0627\u0646" => "dhahran",
            "jubail" or "jubel" or "\u0627\u0644\u062c\u0628\u064a\u0644" or "\u062c\u0628\u064a\u0644" => "jubail",
            "qatif" or "alqatif" or "\u0627\u0644\u0642\u0637\u064a\u0641" or "\u0642\u0637\u064a\u0641" => "qatif",
            "ahsa" or "alahsa" or "alhasa" or "hofuf" or "alhofuf" or "\u0627\u0644\u0627\u062d\u0633\u0627\u0621" or "\u0627\u062d\u0633\u0627\u0621" or "\u0627\u0644\u0647\u0641\u0648\u0641" or "\u0647\u0641\u0648\u0641" => "ahsa",
            "hafr" or "hafralbatn" or "hafralbatin" or "hafrbatin" or "\u062d\u0641\u0631\u0627\u0644\u0628\u0627\u0637\u0646" or "\u062d\u0641\u0631\u0627\u0644\u0628\u0627\u0637\u064a\u0646" or "\u062d\u0641\u0631" => "hafralbatin",
            "rastanura" or "rastanorah" or "\u0631\u0627\u0633\u062a\u0646\u0648\u0631\u0647" or "\u0631\u0627\u0633\u062a\u0646\u0648\u0631\u0627" => "rastanura",
            "khafji" or "alkhafji" or "\u0627\u0644\u062e\u0641\u062c\u064a" or "\u062e\u0641\u062c\u064a" => "khafji",
            "buqayq" or "abqaiq" or "\u0628\u0642\u064a\u0642" => "abqaiq",
            "nairyah" or "nuayriyah" or "\u0627\u0644\u0646\u0639\u064a\u0631\u064a\u0647" or "\u0646\u0639\u064a\u0631\u064a\u0647" => "nairyah",
            "saihat" or "sayhat" or "\u0633\u064a\u0647\u0627\u062a" => "saihat",
            "tarut" or "tarout" or "\u062a\u0627\u0631\u0648\u062a" => "tarut",
            "safwa" or "\u0635\u0641\u0648\u064a" or "\u0635\u0641\u0648\u0627" => "safwa",
            "awamiyah" or "\u0627\u0644\u0639\u0648\u0627\u0645\u064a\u0647" or "\u0639\u0648\u0627\u0645\u064a\u0647" => "awamiyah",
            "rahima" or "rahimah" or "\u0631\u062d\u064a\u0645\u0647" => "rahima",
            "riyadh" or "\u0627\u0644\u0631\u064a\u0627\u0636" or "\u0631\u064a\u0627\u0636" => "riyadh",
            "jeddah" or "\u062c\u062f\u0647" or "\u062c\u062f\u0627" => "jeddah",
            _ => StripLeadingAl(normalized)
        };
    }

    private static string StripLeadingAl(string normalized) =>
        normalized.StartsWith("al", StringComparison.Ordinal) && normalized.Length > 2
            ? normalized[2..]
            : normalized;
}
