using Microsoft.AspNetCore.Http;

namespace Zadana.Api.Security;

/// <summary>
/// HttpOnly refresh-token cookie for the vendor portal. It mirrors the admin
/// cookie policy while keeping a distinct name so admin/vendor sessions cannot
/// overwrite each other in the browser.
/// </summary>
public static class VendorRefreshCookie
{
    public const string CookieName = "zadana_vendor_rt";
    public const string SecureCookieName = "__Host-zadana_vendor_rt";

    public static CookieOptions BuildExpired(IWebHostEnvironment env)
    {
        var options = Build(env);
        options.Expires = DateTimeOffset.UnixEpoch;
        return options;
    }

    public static CookieOptions Build(IWebHostEnvironment env)
    {
        var isProduction = env.IsProduction();

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = CrossOriginCookiePolicy.ResolveSameSite(env),
            Path = isProduction ? "/" : "/api/vendors/auth",
            IsEssential = true
        };
    }

    public static string ResolveCookieName(IWebHostEnvironment env)
        => env.IsProduction() ? SecureCookieName : CookieName;

    public static string? ReadFromRequest(HttpRequest request, IWebHostEnvironment env)
    {
        var name = ResolveCookieName(env);
        if (request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (request.Cookies.TryGetValue(CookieName, out var legacy) && !string.IsNullOrWhiteSpace(legacy))
        {
            return legacy;
        }

        return null;
    }

    public static void Write(HttpResponse response, IWebHostEnvironment env, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        var options = Build(env);
        options.Expires = expiresAtUtc;
        response.Cookies.Append(ResolveCookieName(env), refreshToken, options);
    }

    public static void Clear(HttpResponse response, IWebHostEnvironment env)
    {
        var expired = BuildExpired(env);
        response.Cookies.Append(ResolveCookieName(env), string.Empty, expired);

        var legacyExpired = BuildExpired(env);
        legacyExpired.Path = "/api/vendors/auth";
        response.Cookies.Append(CookieName, string.Empty, legacyExpired);
    }
}
