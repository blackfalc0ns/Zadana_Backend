using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Zadana.Api.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Api.Middleware;

namespace Zadana.Api.Authorization;

public sealed class RequireAccessFilter : IAsyncAuthorizationFilter
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessControlService _accessControlService;
    private readonly string[] _permissions;
    private readonly bool _requireAll;

    public RequireAccessFilter(
        string[] permissions,
        bool requireAll,
        ICurrentUserService currentUserService,
        IAccessControlService accessControlService)
    {
        _permissions = permissions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _requireAll = requireAll;
        _currentUserService = currentUserService;
        _accessControlService = accessControlService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (_permissions.Length == 0 || context.Filters.OfType<IAllowAnonymousFilter>().Any())
        {
            return;
        }

        if (!(_currentUserService.IsAuthenticated && _currentUserService.UserId.HasValue))
        {
            context.Result = new ChallengeResult();
            return;
        }

        var access = await _accessControlService.GetEffectiveAccessAsync(
            _currentUserService.UserId.Value,
            context.HttpContext.RequestAborted);

        var tokenPermissionVersion = context.HttpContext.User.FindFirst("permission_version")?.Value;
        if (!int.TryParse(tokenPermissionVersion, out var claimPermissionVersion) ||
            claimPermissionVersion != access.PermissionVersion)
        {
            var isDev = context.HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (isDev)
            {
                context.Result = new ObjectResult(new
                {
                    code = "PERMISSION_VERSION_MISMATCH",
                    message = ApiLocalizedMessages.Resolve(context.HttpContext, "PERMISSION_VERSION_MISMATCH"),
                    debug_tokenVersion = tokenPermissionVersion ?? "null",
                    debug_dbVersion = access.PermissionVersion,
                    debug_userId = _currentUserService.UserId?.ToString() ?? "null"
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }
            context.Result = new ChallengeResult();
            return;
        }

        var grantedPermissions = access.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isAuthorized = _requireAll
            ? _permissions.All(grantedPermissions.Contains)
            : _permissions.Any(grantedPermissions.Contains);

        // TEMP DEBUG: log permission check details
        if (!isAuthorized)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequireAccessFilter>>();
            logger.LogWarning(
                "ACCESS DENIED for user {UserId}: required=[{Required}], granted=[{Granted}], permissionVersion=token:{TokenVersion}/db:{DbVersion}",
                _currentUserService.UserId,
                string.Join(",", _permissions),
                string.Join(",", grantedPermissions.Take(20)),
                context.HttpContext.User.FindFirst("permission_version")?.Value,
                access.PermissionVersion);
        }

        if (isAuthorized)
        {
            return;
        }

        var isDevelopment = context.HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        var resultObj = new Dictionary<string, object>
        {
            ["code"] = "ACCESS_DENIED",
            ["message"] = ApiLocalizedMessages.Resolve(context.HttpContext, "ACCESS_DENIED"),
            ["requiredPermissions"] = _permissions
        };

        if (isDevelopment)
        {
            resultObj["debug_grantedPermissions"] = grantedPermissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            resultObj["debug_tokenPermissionVersion"] = tokenPermissionVersion ?? "null";
            resultObj["debug_dbPermissionVersion"] = access.PermissionVersion;
            resultObj["debug_userId"] = _currentUserService.UserId?.ToString() ?? "null";
            resultObj["debug_accessScope"] = access.ActiveScope?.RoleCode ?? "null";
        }

        context.Result = new ObjectResult(resultObj)
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
