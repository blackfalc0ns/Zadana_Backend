using System.Globalization;

namespace Zadana.Application.Modules.Geography;

/// <summary>
/// Display labels for stored geography codes (City/Region on VendorBranch, etc.).
/// Storage stays as codes like DAMMAM / EASTERN; UI gets Arabic or English by culture.
/// </summary>
public static class SaudiGeographyDisplay
{
    public static bool PreferArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
        || CultureInfo.CurrentCulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    public static string LocalizeCity(string? city, bool? arabic = null)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return string.Empty;
        }

        var preferArabic = arabic ?? PreferArabic;
        var key = NormalizeKey(city);

        return key switch
        {
            "RIYADH" => preferArabic ? "الرياض" : "Riyadh",
            "JEDDAH" => preferArabic ? "جدة" : "Jeddah",
            "DAMMAM" => preferArabic ? "الدمام" : "Dammam",
            "MAKKAH" or "MECCA" => preferArabic ? "مكة" : "Makkah",
            "MADINAH" or "MEDINA" => preferArabic ? "المدينة" : "Madinah",
            "TAIF" => preferArabic ? "الطائف" : "Taif",
            "TABUK" or "TABUK_CITY" => preferArabic ? "تبوك" : "Tabuk",
            "ABHA" => preferArabic ? "أبها" : "Abha",
            "KHOBAR" or "AL_KHOBAR" => preferArabic ? "الخبر" : "Khobar",
            "QATIF" => preferArabic ? "القطيف" : "Qatif",
            "DHAHRAN" => preferArabic ? "الظهران" : "Dhahran",
            "JUBAIL" => preferArabic ? "الجبيل" : "Jubail",
            "HOFUF" or "AHSA" or "ALAHSA" => preferArabic ? "الهفوف" : "Hofuf",
            "MUBARRAZ" => preferArabic ? "المبرز" : "Mubarraz",
            "KHAFJI" => preferArabic ? "الخفجي" : "Khafji",
            "HAFR_AL_BATIN" or "HAFRALBATIN" => preferArabic ? "حفر الباطن" : "Hafar Al Batin",
            "RAS_TANURA" or "RASTANURA" => preferArabic ? "رأس تنورة" : "Ras Tanura",
            "ABQAIQ" => preferArabic ? "بقيق" : "Abqaiq",
            "NAIRYAH" => preferArabic ? "النعيرية" : "Nairyah",
            "SAIHAT" => preferArabic ? "سيهات" : "Saihat",
            "TARUT" => preferArabic ? "تاروت" : "Tarut",
            "SAFWA" => preferArabic ? "صفوى" : "Safwa",
            "AWAMIYAH" => preferArabic ? "العوامية" : "Awamiyah",
            "RAHIMAH" or "RAHIMA" => preferArabic ? "رحيمة" : "Rahimah",
            "YANBU" => preferArabic ? "ينبع" : "Yanbu",
            "HAIL" or "HAIL_CITY" => preferArabic ? "حائل" : "Hail",
            "JIZAN" or "JAZAN" or "JIZAN_CITY" => preferArabic ? "جازان" : "Jazan",
            "NAJRAN" or "NAJRAN_CITY" => preferArabic ? "نجران" : "Najran",
            "BURAYDAH" or "BURAIDAH" => preferArabic ? "بريدة" : "Buraydah",
            _ => city.Trim()
        };
    }

    public static string LocalizeRegion(string? region, bool? arabic = null)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return string.Empty;
        }

        var preferArabic = arabic ?? PreferArabic;
        var key = NormalizeKey(region);

        return key switch
        {
            "EASTERN" or "EASTERNREGION" or "EASTERNPROVINCE" or "EASTERN_REGION" =>
                preferArabic
                    ? "المنطقة الشرقية (الدمام - الظهران - الخبر)"
                    : "Eastern Region (Dammam, Dhahran, Khobar)",
            "RIYADH" or "RIYADHREGION" or "CENTRAL" =>
                preferArabic ? "منطقة الرياض" : "Riyadh Region",
            "MAKKAH" or "MAKKAHREGION" or "WESTERN" =>
                preferArabic ? "منطقة مكة" : "Makkah Region",
            "MADINAH" or "MADINAHREGION" =>
                preferArabic ? "منطقة المدينة" : "Madinah Region",
            "EASTERN REGION" => preferArabic
                ? "المنطقة الشرقية (الدمام - الظهران - الخبر)"
                : "Eastern Region (Dammam, Dhahran, Khobar)",
            _ => region.Trim()
        };
    }

    public static string FormatBranchAddress(
        string? addressLine,
        string? city,
        string? region,
        bool? arabic = null)
    {
        var preferArabic = arabic ?? PreferArabic;
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(addressLine) ? null : addressLine.Trim(),
            LocalizeCity(city, preferArabic),
            LocalizeRegion(region, preferArabic)
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(preferArabic ? "، " : ", ", parts);
    }

    private static string NormalizeKey(string value) =>
        value.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
}
