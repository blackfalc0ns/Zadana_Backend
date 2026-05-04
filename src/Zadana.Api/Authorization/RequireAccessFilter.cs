using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;

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

        var grantedPermissions = access.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isAuthorized = _requireAll
            ? _permissions.All(grantedPermissions.Contains)
            : _permissions.Any(grantedPermissions.Contains);

        if (isAuthorized)
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            code = "ACCESS_DENIED",
            message = "You do not have permission to perform this action.",
            requiredPermissions = _permissions
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
