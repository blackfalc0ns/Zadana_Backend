using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Api.Middleware;

/// <summary>
/// After the JWT bearer middleware runs, double-check the resolved identity
/// against the revocation store. Two cases:
///   1. Specific JTI revoked  (logout, suspicious activity).
///   2. All tokens issued for a user before a cutoff (admin ban, password
///      reset, refresh-token reuse-detection trigger).
///
/// This middleware runs after UseAuthentication so the principal is
/// populated, but before UseAuthorization so a 401 is returned cleanly.
/// </summary>
public sealed class JwtRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public JwtRevocationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IJwtRevocationStore revocationStore)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (!string.IsNullOrEmpty(jti) &&
            await revocationStore.IsRevokedAsync(jti, context.RequestAborted))
        {
            await Reject(context, "TOKEN_REVOKED");
            return;
        }

        var sub = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(sub, out var userId))
        {
            // The 'iat' claim is unix-seconds when present (System.IdentityModel
            // doesn't always emit it in seconds, so fall back to the token's
            // ValidFrom which JwtBearer hands us via the AuthenticationTicket).
            var iat = TryGetIssuedAtUtc(context);
            if (iat.HasValue && await revocationStore.IsUserRevokedAsync(userId, iat.Value, context.RequestAborted))
            {
                await Reject(context, "USER_TOKENS_REVOKED");
                return;
            }
        }

        await _next(context);
    }

    private static DateTime? TryGetIssuedAtUtc(HttpContext context)
    {
        var iatClaim = context.User.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        if (long.TryParse(iatClaim, out var iatUnix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;
        }

        // Fall back to nbf if iat is missing.
        var nbfClaim = context.User.FindFirst(JwtRegisteredClaimNames.Nbf)?.Value;
        if (long.TryParse(nbfClaim, out var nbfUnix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(nbfUnix).UtcDateTime;
        }

        return null;
    }

    private static Task Reject(HttpContext context, string code)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            code,
            message = "The bearer token has been revoked. Please sign in again."
        }, context.RequestAborted);
    }
}
