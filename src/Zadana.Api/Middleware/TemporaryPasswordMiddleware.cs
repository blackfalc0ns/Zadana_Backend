using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.Middleware;

public sealed class TemporaryPasswordMiddleware
{
    private static readonly PathString[] AllowedPaths =
    [
        "/api/admin/auth/me",
        "/api/admin/auth/logout",
        "/api/admin/auth/change-temporary-password",
        "/api/admin/auth/refresh-token",
        "/api/admin/auth/csrf"
    ];

    private readonly RequestDelegate _next;

    public TemporaryPasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, ApplicationDbContext dbContext)
    {
        if (IsAllowed(httpContext.Request.Path) ||
            httpContext.User.Identity?.IsAuthenticated != true)
        {
            await _next(httpContext);
            return;
        }

        var idClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(idClaim, out var userId))
        {
            await _next(httpContext);
            return;
        }

        var mustChangePassword = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.MustChangePassword)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (!mustChangePassword)
        {
            await _next(httpContext);
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            code = "TEMP_PASSWORD_CHANGE_REQUIRED",
            message = "You must change your temporary password before continuing."
        }, httpContext.RequestAborted);
    }

    private static bool IsAllowed(PathString path) =>
        AllowedPaths.Any(allowed => path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase));
}
