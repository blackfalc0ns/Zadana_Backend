using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Services;

namespace Zadana.Application.Modules.Identity.Commands.UpdateRole;

public record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    UserRole IdentityRole,
    PanelScope PanelScope,
    List<string> Permissions
) : IRequest<RoleDefinitionDto>;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDefinitionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAdminAccessValidationService _validationService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateRoleCommandHandler(
        IApplicationDbContext context,
        IAdminAccessValidationService validationService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _validationService = validationService;
        _currentUserService = currentUserService;
    }

    public async Task<RoleDefinitionDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var roleName = NormalizeName(request.Name);
        var description = NormalizeDescription(request.Description);
        var permissions = NormalizePermissions(request.Permissions);

        var role = await _context.RoleDefinitions
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.PermissionDefinition)
            .Include(r => r.UserAccessScopes)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role == null)
        {
            throw new NotFoundException(nameof(RoleDefinition), request.Id);
        }

        if (role.IsSystem)
        {
            throw new BadRequestException("CANNOT_UPDATE_SYSTEM_ROLE", "System roles cannot be modified.");
        }

        AccessRoleGuard.EnsureRoleMatchesPanelScope(request.IdentityRole, request.PanelScope);

        var assignedUserIds = role.UserAccessScopes
            .Where(scope => scope.IsActive)
            .Select(scope => scope.UserId)
            .Distinct()
            .ToList();

        if (assignedUserIds.Count > 0 &&
            (role.IdentityRole != request.IdentityRole || role.PanelScope != request.PanelScope))
        {
            throw new BadRequestException(
                "ROLE_ASSIGNMENT_SCOPE_LOCKED",
                "Identity role and panel scope cannot be changed while the role is assigned to active users.");
        }

        var permissionDefs = await _context.PermissionDefinitions
            .Where(p => permissions.Contains(p.Key))
            .ToListAsync(cancellationToken);
        var invalidPermissions = permissions
            .Except(permissionDefs.Select(p => p.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invalidPermissions.Count > 0)
        {
            throw new BadRequestException("INVALID_PERMISSION", $"Unknown permission keys: {string.Join(", ", invalidPermissions)}");
        }

        var wrongScopePermissions = permissionDefs
            .Where(p => p.PanelScope != request.PanelScope)
            .Select(p => p.Key)
            .ToList();
        if (wrongScopePermissions.Count > 0)
        {
            throw new BadRequestException(
                "INVALID_PERMISSION_SCOPE",
                $"Permissions do not belong to the selected panel scope: {string.Join(", ", wrongScopePermissions)}");
        }

        var currentPermissionKeys = role.RolePermissions
            .Select(permission => permission.PermissionDefinition.Key)
            .ToList();
        var requiresElevatedRoleChange = role.IdentityRole == UserRole.SuperAdmin ||
            request.IdentityRole == UserRole.SuperAdmin;
        var roleDefinitionPermissions = currentPermissionKeys
            .Concat(permissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        await _validationService.EnsureCanManageRoleDefinitionAsync(
            request.IdentityRole,
            request.PanelScope,
            roleDefinitionPermissions,
            cancellationToken,
            requiresElevatedRoleChange,
            role.Id.ToString());

        await EnsureActorWillKeepAdministrativeAccessAsync(
            role.Id,
            request.IdentityRole,
            permissionDefs.Select(permission => permission.Key).ToList(),
            cancellationToken);

        role.Update(
            name: roleName,
            identityRole: request.IdentityRole,
            panelScope: request.PanelScope,
            isSystem: role.IsSystem,
            isActive: role.IsActive,
            description: description
        );

        // Update permissions
        _context.RolePermissions.RemoveRange(role.RolePermissions);

        foreach (var permissionDef in permissionDefs)
        {
            role.RolePermissions.Add(new RolePermission(role.Id, permissionDef.Id));
        }

        if (assignedUserIds.Count > 0)
        {
            var users = await _context.Users
                .Where(user => assignedUserIds.Contains(user.Id))
                .ToListAsync(cancellationToken);
            foreach (var user in users)
            {
                user.IncrementPermissionVersion();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new RoleDefinitionDto(
            Id: role.Id,
            Code: role.Code,
            Name: role.Name,
            Description: role.Description,
            IsSystem: role.IsSystem,
            IsActive: role.IsActive,
            IdentityRole: role.IdentityRole,
            PanelScope: role.PanelScope,
            Permissions: permissionDefs.Select(p => p.Key).OrderBy(x => x).ToList(),
            UsersCount: role.UserAccessScopes.Count
        );
    }

    private async Task EnsureActorWillKeepAdministrativeAccessAsync(
        Guid roleId,
        UserRole requestedIdentityRole,
        IReadOnlyCollection<string> requestedPermissions,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return;
        }

        var actorUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == _currentUserService.UserId.Value, cancellationToken);
        if (actorUser is null)
        {
            return;
        }

        var actorScope = await _context.UserAccessScopes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                scope => scope.UserId == _currentUserService.UserId.Value && scope.IsActive,
                cancellationToken);

        if (actorScope is not null && actorScope.RoleDefinitionId != roleId)
        {
            return;
        }

        if (actorScope is null)
        {
            var currentFallbackRoleId = await ResolveActorFallbackRoleIdAsync(actorUser.Role, roleId: null, requestedIdentityRole: null, cancellationToken);
            if (currentFallbackRoleId != roleId)
            {
                return;
            }

            var fallbackRoleAfterChange = await ResolveActorFallbackRoleIdAsync(actorUser.Role, roleId, requestedIdentityRole, cancellationToken);
            if (fallbackRoleAfterChange != roleId)
            {
                return;
            }
        }

        var effectivePermissions = requestedPermissions
            .Where(IsAdministrativeAccessPermission)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var overrides = await _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(overrideEntry => overrideEntry.UserId == _currentUserService.UserId.Value && overrideEntry.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var overrideEntry in overrides.Where(entry => IsAdministrativeAccessPermission(entry.PermissionKey)))
        {
            if (overrideEntry.Mode == PermissionOverrideMode.Grant)
            {
                effectivePermissions.Add(overrideEntry.PermissionKey);
                continue;
            }

            effectivePermissions.Remove(overrideEntry.PermissionKey);
        }

        if (effectivePermissions.Count == 0)
        {
            throw new BadRequestException(
                "SELF_ACCESS_CHANGE_BLOCKED",
                "You cannot remove your own administrative access through the active role definition.");
        }
    }

    private async Task<Guid?> ResolveActorFallbackRoleIdAsync(
        UserRole actorRole,
        Guid? roleId,
        UserRole? requestedIdentityRole,
        CancellationToken cancellationToken)
    {
        var roles = await _context.RoleDefinitions
            .AsNoTracking()
            .Where(role => role.IsActive && role.IdentityRole == actorRole || role.Id == roleId)
            .Select(role => new
            {
                role.Id,
                role.Code,
                role.IdentityRole,
                role.IsActive,
                role.IsSystem
            })
            .ToListAsync(cancellationToken);

        var candidateRoles = roles
            .Select(role => new
            {
                role.Id,
                role.Code,
                IdentityRole = role.Id == roleId && requestedIdentityRole.HasValue
                    ? requestedIdentityRole.Value
                    : role.IdentityRole,
                role.IsActive,
                role.IsSystem
            })
            .Where(role => role.IsActive && role.IdentityRole == actorRole)
            .ToList();

        var preferredCode = IdentityRoleDefaults.ResolvePreferredRoleCode(actorRole);
        var preferred = candidateRoles.FirstOrDefault(role =>
            string.Equals(role.Code, preferredCode, StringComparison.OrdinalIgnoreCase));
        if (preferred is not null)
        {
            return preferred.Id;
        }

        return candidateRoles
            .OrderByDescending(role => role.IsSystem)
            .ThenBy(role => role.Code, StringComparer.OrdinalIgnoreCase)
            .Select(role => (Guid?)role.Id)
            .FirstOrDefault();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("ROLE_NAME_REQUIRED", "Role name is required.");
        }

        var normalized = name.Trim();
        if (normalized.Length > 200)
        {
            throw new BadRequestException("ROLE_NAME_TOO_LONG", "Role name must be 200 characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalized = description.Trim();
        if (normalized.Length > 500)
        {
            throw new BadRequestException("ROLE_DESCRIPTION_TOO_LONG", "Role description must be 500 characters or fewer.");
        }

        return normalized;
    }

    private static List<string> NormalizePermissions(IEnumerable<string>? permissions) =>
        (permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsAdministrativeAccessPermission(string permission) =>
        string.Equals(permission, PermissionKeys.Admin.UsersAccessView, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessCreate, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessEdit, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessApprove, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessManageSettings, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.EmailCenterView, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.EmailCenterEdit, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.EmailCenterApprove, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.EmailCenterManageSettings, StringComparison.OrdinalIgnoreCase);

}
