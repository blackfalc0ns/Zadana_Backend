using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Localization;

namespace Zadana.Api.Localization;

public static class ApiLocalizedMessages
{
    public static string Resolve(HttpContext? context, string key, string? fallback = null)
    {
        var value = PrefersEnglish(context)
            ? LocalizedMessages.GetEn(key)
            : LocalizedMessages.GetAr(key);

        return string.Equals(value, key, StringComparison.Ordinal)
            ? fallback ?? key
            : value;
    }

    private static bool PrefersEnglish(HttpContext? context)
    {
        var language = context?.Request?.Headers["Accept-Language"].ToString().ToLowerInvariant() ?? string.Empty;
        return language.Contains("en");
    }
}
