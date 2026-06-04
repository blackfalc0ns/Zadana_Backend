namespace Zadana.Application.Modules.Geography;

/// <summary>
/// Canonical lowercase alias keys aligned with checkout city matching.
/// </summary>
public static class GeographyCityAliases
{
    public static string? MapAliasKey(string normalizedKey) =>
        normalizedKey switch
        {
            "الدمام" or "دمام" or "dammam" => "dammam",
            "الرياض" or "رياض" or "riyadh" => "riyadh",
            "جده" or "جدة" or "جدا" or "jeddah" => "jeddah",
            "مكه" or "مكة" or "mecca" or "makkah" => "makkah",
            "المدينه" or "المدينة" or "مدينه" or "مدينة" or "madinah" or "medina" => "madinah",
            "الخبر" or "خبر" or "khobar" or "alkhobar" => "khobar",
            "الظهران" or "ظهران" or "dhahran" => "dhahran",
            "الجبيل" or "جبيل" or "jubail" or "jubel" => "jubail",
            "القطيف" or "قطيف" or "qatif" or "alqatif" => "qatif",
            "الاحساء" or "الأحساء" or "احساء" or "أحساء" or "الهفوف" or "هفوف" or "ahsa" or "alahsa" or "alhasa" or "hofuf" or "alhofuf" => "ahsa",
            "حفرالباطن" or "حفر" or "hafr" or "hafralbatn" or "hafr_al_batin" or "hafralbatin" => "hafralbatin",
            "رأستنورة" or "راستنورة" or "رأستنوره" or "rastanura" or "rastanorah" => "rastanura",
            "الخفجي" or "خفجي" or "khafji" or "alkhafji" => "khafji",
            "بقيق" or "buqayq" or "abqaiq" => "abqaiq",
            "النعيرية" or "نعيرية" or "nairyah" or "nuayriyah" => "nairyah",
            "سيهات" or "saihat" or "sayhat" => "saihat",
            "تاروت" or "tarut" or "tarout" => "tarut",
            "صفوى" or "صفوا" or "safwa" => "safwa",
            "العوامية" or "عوامية" or "awamiyah" => "awamiyah",
            "رحيمة" or "rahima" or "rahimah" => "rahima",
            "الطائف" or "طائف" or "taif" => "taif",
            "تبوك" or "tabuk" => "tabuk",
            "ابها" or "أبها" or "abha" => "abha",
            "حائل" or "حايل" or "hail" or "ha'il" => "hail",
            "جازان" or "جيزان" or "jazan" or "jizan" => "jazan",
            "نجران" or "najran" => "najran",
            "بريده" or "بريدة" or "buraidah" or "buraydah" => "buraidah",
            "ينبع" or "yanbu" or "yanbuu" => "yanbu",
            _ => normalizedKey
        };

    public static IReadOnlyDictionary<string, string> ExplicitAliasCityCodes { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ahsa"] = "HOFUF",
            ["alahsa"] = "HOFUF",
            ["alhasa"] = "HOFUF",
            ["hofuf"] = "HOFUF",
            ["alhofuf"] = "HOFUF",
            ["tabuk"] = "TABUK_CITY",
            ["hail"] = "HAIL_CITY",
            ["ha'il"] = "HAIL_CITY",
            ["jazan"] = "JIZAN_CITY",
            ["jizan"] = "JIZAN_CITY",
            ["najran"] = "NAJRAN_CITY",
            ["buraidah"] = "BURAYDAH",
            ["buraydah"] = "BURAYDAH",
            ["madinah"] = "MADINAH",
            ["medina"] = "MADINAH",
            ["makkah"] = "MAKKAH",
            ["mecca"] = "MAKKAH",
            ["khobar"] = "KHOBAR",
            ["alkhobar"] = "KHOBAR",
            ["riyadh"] = "RIYADH",
            ["jeddah"] = "JEDDAH",
            ["dammam"] = "DAMMAM",
            ["taif"] = "TAIF",
            ["abha"] = "ABHA",
            ["yanbu"] = "YANBU",
            ["jubail"] = "JUBAIL",
            ["qatif"] = "QATIF",
            ["dhahran"] = "DHAHRAN",
            ["khafji"] = "KHAFJI",
            ["hafralbatin"] = "HAFR_AL_BATIN",
            ["hafralbatn"] = "HAFR_AL_BATIN",
            ["hafr_al_batin"] = "HAFR_AL_BATIN",
            ["rastanura"] = "RAS_TANURA",
            ["rastanorah"] = "RAS_TANURA",
            ["abqaiq"] = "ABQAIQ",
            ["buqayq"] = "ABQAIQ",
            ["nairyah"] = "NAIRYAH",
            ["nuayriyah"] = "NAIRYAH",
            ["saihat"] = "SAIHAT",
            ["sayhat"] = "SAIHAT",
            ["tarut"] = "TARUT",
            ["tarout"] = "TARUT",
            ["safwa"] = "SAFWA",
            ["awamiyah"] = "AWAMIYAH",
            ["rahima"] = "RAHIMAH",
            ["rahimah"] = "RAHIMAH",
            ["diriyah"] = "DIRIYAH",
            ["khamismushait"] = "KHAMIS_MUSHAIT",
            ["unayzah"] = "UNAYZAH",
            ["sakaka"] = "SAKAKA",
            ["arar"] = "ARAR",
            ["ula"] = "ULA",
            ["alula"] = "ULA"
        };
}
