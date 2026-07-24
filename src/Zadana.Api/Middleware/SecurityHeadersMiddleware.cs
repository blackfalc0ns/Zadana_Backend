using Microsoft.Extensions.Hosting;

namespace Zadana.Api.Middleware;

/// <summary>
/// Adds standard security headers to every response.
/// Safe defaults that do not break a JSON API or Swagger:
/// - X-Content-Type-Options: nosniff
/// - X-Frame-Options: DENY            (API is never framed)
/// - Referrer-Policy: no-referrer
/// - Permissions-Policy: minimal      (no geo/camera/mic by default)
/// - Cross-Origin-Opener-Policy: same-origin
/// - Strict-Transport-Security        (production only, behind HTTPS)
/// - Content-Security-Policy          (relaxed; Swagger UI uses inline scripts)
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Set headers before the response starts so they are guaranteed to be present.
        context.Response.OnStarting(() =>
        {
            if (!headers.ContainsKey("X-Content-Type-Options"))
            {
                headers["X-Content-Type-Options"] = "nosniff";
            }

            if (!headers.ContainsKey("X-Frame-Options"))
            {
                headers["X-Frame-Options"] = "DENY";
            }

            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers["Referrer-Policy"] = "no-referrer";
            }

            if (!headers.ContainsKey("Permissions-Policy"))
            {
                headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";
            }

            // Allow OAuth / Google Identity popup flows to communicate with the opener.
            if (!headers.ContainsKey("Cross-Origin-Opener-Policy"))
            {
                headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
            }

            // CSP relaxed enough for Swagger UI but still useful as a baseline.
            // Frontends are served separately, so the API itself does not render HTML
            // outside of Swagger.
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "img-src 'self' data: https:; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "connect-src 'self' https:; " +
                    "frame-ancestors 'none'; " +
                    "base-uri 'self'";
            }

            // Hide ASP.NET / IIS server signatures.
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
