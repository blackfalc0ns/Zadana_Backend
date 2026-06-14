namespace Zadana.Api.Security;

/// <summary>
/// Admin/vendor SPAs are hosted on Vercel while the API runs on runasp.net.
/// SameSite=Strict blocks antiforgery and refresh cookies on cross-site XHR
/// with credentials, which breaks admin login (400 INVALID_CSRF_TOKEN).
/// </summary>
public static class CrossOriginCookiePolicy
{
    public static SameSiteMode ResolveSameSite(IHostEnvironment environment) =>
        environment.IsProduction() ? SameSiteMode.None : SameSiteMode.Strict;
}
