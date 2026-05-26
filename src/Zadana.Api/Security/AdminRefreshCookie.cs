using Microsoft.AspNetCore.Http;

namespace Zadana.Api.Security;

/// <summary>
/// Centralised configuration for the admin refresh-token cookie.
/// The refresh token is the highest-value secret a client can hold (anyone
/// possessing it can mint new access tokens for the user's lifetime), so we
/// store it in an <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Strict</c> cookie
/// scoped strictly to the admin auth path.
/// </summary>
public static class AdminRefreshCookie
{
    /// <summary>
    /// Name of the refresh-token cookie used by the admin panel.
    /// Prefixed with <c>__Host-</c> in production to enable browser-side
    /// integrity protections (no Domain attribute, must be Secure, must be
    /// served from path "/").
    /// </summary>
    public const string CookieName = "zadana_admin_rt";
    public const string SecureCookieName = "__Host-zadana_admin_rt";

    /// <summary>
    /// Cookie returned on logout — clears the refresh token.
    /// </summary>
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
            SameSite = SameSiteMode.Strict,
            // In production we use __Host- prefix → cookie must be on path "/"
            // and have no Domain attribute. In dev we keep a narrow path so
            // accidental requests from other parts of the API can't leak it.
            Path = isProduction ? "/" : "/api/admin/auth",
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

        // Fallback: accept the non-prefixed name during cross-environment
        // transitions (e.g., a dev cookie carried into a hosted preview).
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

        // Also clear the legacy name to defeat stale cookies.
        var legacyExpired = BuildExpired(env);
        legacyExpired.Path = "/api/admin/auth";
        response.Cookies.Append(CookieName, string.Empty, legacyExpired);
    }
}
