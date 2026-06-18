using System.Security.Cryptography;
using System.Text;

namespace Zadana.Api.Security;

/// <summary>
/// Stateless double-submit CSRF protection for the cross-origin admin SPA.
/// The API returns the token in the response body and stores the same value
/// in a host-only HttpOnly cookie. State-changing requests must echo the
/// response token in the X-XSRF-TOKEN header.
/// </summary>
public static class ApiCsrfToken
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public static string Issue(HttpResponse response, IHostEnvironment environment)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        response.Cookies.Append(CookieName(environment), token, CreateCookieOptions(environment));
        return token;
    }

    public static bool IsValid(HttpRequest request, IHostEnvironment environment)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return false;
        }

        var headerToken = headerValues.ToString();
        var cookieToken = request.Cookies[CookieName(environment)];
        if (string.IsNullOrWhiteSpace(headerToken) || string.IsNullOrWhiteSpace(cookieToken))
        {
            return false;
        }

        var headerBytes = Encoding.UTF8.GetBytes(headerToken);
        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        return headerBytes.Length == cookieBytes.Length
            && CryptographicOperations.FixedTimeEquals(headerBytes, cookieBytes);
    }

    public static void Clear(HttpResponse response, IHostEnvironment environment)
    {
        response.Cookies.Delete(CookieName(environment), CreateCookieOptions(environment));

        // Remove cookies issued by the previous ASP.NET Antiforgery flow.
        response.Cookies.Delete(environment.IsProduction() ? "__Host-XSRF-AF" : "XSRF-AF");
        response.Cookies.Delete("XSRF-TOKEN");
    }

    private static string CookieName(IHostEnvironment environment) =>
        environment.IsProduction() ? "__Host-XSRF-TOKEN" : "XSRF-TOKEN";

    private static CookieOptions CreateCookieOptions(IHostEnvironment environment) =>
        new()
        {
            HttpOnly = true,
            Secure = environment.IsProduction(),
            SameSite = CrossOriginCookiePolicy.ResolveSameSite(environment),
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddHours(2)
        };
}
