using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.UpdateUserOverrides;

public record UpdateUserOverridesCommand(
    Guid UserId,
    List<string> GrantedPermissions,
    List<string> RevokedPermissions
) : IRequest;

public class UpdateUserOverridesCommandHandler : IRequestHandler<UpdateUserOverridesCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAccessValidationService _validationService;
    private readonly IAccessAuditService _auditService;

    public UpdateUserOverridesCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAdminAccessValidationService validationService,
        IAccessAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _validationService = validationService;
        _auditService = auditService;
    }

    public async Task Handle(UpdateUserOverridesCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
            throw new NotFoundException(nameof(User), request.UserId);

        var grantedPermissions = Normalize(request.GrantedPermissions);
        var revokedPermissions = Normalize(request.RevokedPermissions);

        var activeScope = await _context.UserAccessScopes
            .Include(scope => scope.RoleDefinition)
            .FirstOrDefaultAsync(scope => scope.UserId == request.UserId && scope.IsActive, cancellationToken);

        var panelScope = activeScope?.PanelScope ?? AccessRoleGuard.ResolvePanelScope(user.Role);
        var role = activeScope?.RoleDefinition;
        if (role is null)
        {
            role = await _context.RoleDefinitions
                .Where(item => item.IdentityRole == user.Role && item.PanelScope == panelScope && item.IsActive)
                .OrderByDescending(item => item.IsSystem)
                .ThenBy(item => item.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (role is null)
        {
            throw new BadRequestException("ROLE_REQUIRED", "The user does not have an active role definition.");
        }

        AccessRoleGuard.EnsureRoleCanBeAssignedToUser(user, role);

        await _validationService.EnsureCanMutateUserAccessAsync(
            targetUserId: user.Id,
            targetRole: user.Role,
            requestedStatus: null,
            actorUserId: _currentUserService.UserId,
            newRole: role,
            grantedPermissions: grantedPermissions,
            revokedPermissions: revokedPermissions,
            cancellationToken);

        await _validationService.ValidatePermissionOverridesAsync(
            panelScope,
            grantedPermissions,
            revokedPermissions,
            cancellationToken);

        // Remove all existing overrides for this user
        var existingOverrides = await _context.UserPermissionOverrides
            .Where(o => o.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        _context.UserPermissionOverrides.RemoveRange(existingOverrides);

        // Add granted overrides
        foreach (var key in grantedPermissions)
        {
            _context.UserPermissionOverrides.Add(
                new UserPermissionOverride(request.UserId, key, PermissionOverrideMode.Grant));
        }

        // Add revoked overrides
        foreach (var key in revokedPermissions)
        {
            _context.UserPermissionOverrides.Add(
                new UserPermissionOverride(request.UserId, key, PermissionOverrideMode.Revoke));
        }

        user.IncrementPermissionVersion();
        _auditService.Add(
            user.Id,
            "permission-overrides-updated",
            "Permission overrides were updated.",
            after: new
            {
                user.Id,
                PanelScope = panelScope.ToString(),
                GrantedPermissions = grantedPermissions,
                RevokedPermissions = revokedPermissions
            });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<string> Normalize(IEnumerable<string>? permissions) =>
        (permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
