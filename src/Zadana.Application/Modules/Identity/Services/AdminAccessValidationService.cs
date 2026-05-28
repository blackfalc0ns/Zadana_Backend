using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

public interface IAdminAccessValidationService
{
    Task<Guid?> NormalizeAndValidateScopeAsync(
        RoleDefinition role,
        PanelScope panelScope,
        AccessScopeType scopeType,
        Guid? scopeEntityId,
        Guid userId,
        CancellationToken cancellationToken);

    Task ValidatePermissionOverridesAsync(
        PanelScope panelScope,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions,
        CancellationToken cancellationToken);

    Task EnsureCanManageRoleDefinitionAsync(
        UserRole identityRole,
        PanelScope panelScope,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken);

    Task EnsureCanMutateUserAccessAsync(
        Guid targetUserId,
        UserRole targetRole,
        string? requestedStatus,
        Guid? actorUserId,
        RoleDefinition newRole,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions,
        CancellationToken cancellationToken);
}

public sealed class AdminAccessValidationService : IAdminAccessValidationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessControlService _accessControlService;

    public AdminAccessValidationService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAccessControlService accessControlService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _accessControlService = accessControlService;
    }

    public async Task<Guid?> NormalizeAndValidateScopeAsync(
        RoleDefinition role,
        PanelScope panelScope,
        AccessScopeType scopeType,
        Guid? scopeEntityId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        AccessRoleGuard.EnsureRoleMatchesPanelScope(role.IdentityRole, role.PanelScope);

        if (role.PanelScope != panelScope)
        {
            throw new BadRequestException("ROLE_SCOPE_MISMATCH", "The selected role does not belong to the requested panel scope.");
        }

        var normalizedEntityId = scopeEntityId;

        switch (panelScope)
        {
            case PanelScope.SuperAdminPanel:
                if (scopeType != AccessScopeType.Global)
                {
                    throw new BadRequestException("ROLE_SCOPE_MISMATCH", "Super admin panel users must use the global access scope.");
                }
                return null;

            case PanelScope.VendorPanel:
                if (scopeType is not (AccessScopeType.VendorCompany or AccessScopeType.VendorBranch) || !scopeEntityId.HasValue)
                {
                    throw new BadRequestException("INVALID_SCOPE_ENTITY", "Vendor panel users must be scoped to an existing vendor or branch.");
                }

                var vendorExists = scopeType == AccessScopeType.VendorCompany
                    ? await _context.Vendors.AnyAsync(x => x.Id == scopeEntityId.Value, cancellationToken)
                    : await _context.VendorBranches.AnyAsync(x => x.Id == scopeEntityId.Value, cancellationToken);
                if (!vendorExists)
                {
                    throw new BadRequestException("INVALID_SCOPE_ENTITY", "The selected vendor scope entity does not exist.");
                }
                return normalizedEntityId;

            case PanelScope.DriverApp:
                if (scopeType != AccessScopeType.DriverSelf || !scopeEntityId.HasValue)
                {
                    throw new BadRequestException("INVALID_SCOPE_ENTITY", "Driver app users must be scoped to an existing driver.");
                }

                var driverExists = await _context.Drivers.AnyAsync(x => x.Id == scopeEntityId.Value, cancellationToken);
                if (!driverExists)
                {
                    throw new BadRequestException("INVALID_SCOPE_ENTITY", "The selected driver scope entity does not exist.");
                }
                return normalizedEntityId;

            case PanelScope.CustomerApp:
                if (scopeType != AccessScopeType.CustomerSelf)
                {
                    throw new BadRequestException("INVALID_SCOPE_ENTITY", "Customer app users must use the customer self scope.");
                }
                return userId;

            default:
                throw new BadRequestException("ROLE_SCOPE_MISMATCH", "Unsupported panel scope.");
        }
    }

    public async Task ValidatePermissionOverridesAsync(
        PanelScope panelScope,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions,
        CancellationToken cancellationToken)
    {
        var grants = Normalize(grantedPermissions);
        var revokes = Normalize(revokedPermissions);
        var duplicates = grants.Intersect(revokes, StringComparer.OrdinalIgnoreCase).ToList();
        if (duplicates.Count > 0)
        {
            throw new BadRequestException(
                "INVALID_PERMISSION_SCOPE",
                $"Permissions cannot be both granted and revoked: {string.Join(", ", duplicates)}");
        }

        var requested = grants.Concat(revokes).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (requested.Count == 0)
        {
            return;
        }

        var permissionDefs = await _context.PermissionDefinitions
            .Where(x => requested.Contains(x.Key))
            .Select(x => new { x.Key, x.PanelScope })
            .ToListAsync(cancellationToken);

        var invalid = requested
            .Except(permissionDefs.Select(x => x.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (invalid.Count > 0)
        {
            throw new BadRequestException("INVALID_PERMISSION", $"Unknown permission keys: {string.Join(", ", invalid)}");
        }

        var wrongScope = permissionDefs
            .Where(x => x.PanelScope != panelScope)
            .Select(x => x.Key)
            .ToList();
        if (wrongScope.Count > 0)
        {
            throw new BadRequestException(
                "INVALID_PERMISSION_SCOPE",
                $"Permissions do not belong to the selected panel scope: {string.Join(", ", wrongScope)}");
        }
    }

    public async Task EnsureCanManageRoleDefinitionAsync(
        UserRole identityRole,
        PanelScope panelScope,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        AccessRoleGuard.EnsureRoleMatchesPanelScope(identityRole, panelScope);

        if (!await RequiresElevatedAccessAsync(identityRole, permissions, cancellationToken))
        {
            return;
        }

        await EnsureActorHasAnyPermissionAsync(
            [
                PermissionKeys.Admin.UsersAccessManageSettings
            ],
            "SENSITIVE_ROLE_CHANGE_REQUIRES_MANAGE_SETTINGS",
            "Managing super-admin roles or roles with sensitive permissions requires access-management settings permission.",
            cancellationToken);
    }

    public async Task EnsureCanMutateUserAccessAsync(
        Guid targetUserId,
        UserRole targetRole,
        string? requestedStatus,
        Guid? actorUserId,
        RoleDefinition newRole,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions,
        CancellationToken cancellationToken)
    {
        var isTargetSuperAdmin = targetRole == UserRole.SuperAdmin;
        var targetWillRemainActive = requestedStatus?.Trim().ToLowerInvariant() is not ("suspended" or "inactive");
        var targetWillRemainSuperAdmin = newRole.IdentityRole == UserRole.SuperAdmin;

        if (isTargetSuperAdmin && (!targetWillRemainActive || !targetWillRemainSuperAdmin))
        {
            var activeSuperAdminCount = await _context.Users.CountAsync(
                x => x.Role == UserRole.SuperAdmin && x.AccountStatus == AccountStatus.Active,
                cancellationToken);

            if (activeSuperAdminCount <= 1)
            {
                throw new BadRequestException("LAST_SUPER_ADMIN_PROTECTED", "The last active super admin cannot be disabled or downgraded.");
            }
        }

        var rolePermissionKeys = await _context.RolePermissions
            .AsNoTracking()
            .Where(item => item.RoleDefinitionId == newRole.Id)
            .Join(
                _context.PermissionDefinitions.AsNoTracking(),
                rolePermission => rolePermission.PermissionDefinitionId,
                permission => permission.Id,
                (_, permission) => permission.Key)
            .ToListAsync(cancellationToken);

        var requestedPermissionKeys = Normalize(grantedPermissions)
            .Concat(Normalize(revokedPermissions))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (await RequiresElevatedAccessAsync(
                newRole.IdentityRole,
                rolePermissionKeys.Concat(requestedPermissionKeys).ToList(),
                cancellationToken))
        {
            await EnsureActorHasAnyPermissionAsync(
                [
                    PermissionKeys.Admin.UsersAccessApprove,
                    PermissionKeys.Admin.UsersAccessManageSettings
                ],
                "SENSITIVE_ACCESS_CHANGE_REQUIRES_APPROVAL",
                "Assigning super-admin access or sensitive permissions requires access approval permission.",
                cancellationToken);
        }

        if (actorUserId.HasValue && actorUserId.Value == targetUserId)
        {
            var statusBlocksSelf = requestedStatus?.Trim().ToLowerInvariant() is "suspended" or "inactive";
            var downgradesSelf = targetRole == UserRole.SuperAdmin && newRole.IdentityRole != UserRole.SuperAdmin;
            var revokesAccessSettings = revokedPermissions.Any(IsAdministrativeAccessPermission);
            var newEffectivePermissions = rolePermissionKeys
                .Concat(grantedPermissions)
                .Where(permission => !revokedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removesOwnAccessManagement = !newEffectivePermissions.Any(IsAdministrativeAccessPermission);

            if (statusBlocksSelf || downgradesSelf || revokesAccessSettings || removesOwnAccessManagement)
            {
                throw new BadRequestException("SELF_ACCESS_CHANGE_BLOCKED", "You cannot remove your own administrative access.");
            }
        }
    }

    private static List<string> Normalize(IReadOnlyCollection<string> permissions) =>
        permissions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<bool> RequiresElevatedAccessAsync(
        UserRole identityRole,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken)
    {
        if (identityRole == UserRole.SuperAdmin)
        {
            return true;
        }

        var requested = Normalize(permissionKeys);
        if (requested.Count == 0)
        {
            return false;
        }

        return await _context.PermissionDefinitions
            .AsNoTracking()
            .AnyAsync(
                permission => requested.Contains(permission.Key) && permission.IsSensitive,
                cancellationToken);
    }

    private async Task EnsureActorHasAnyPermissionAsync(
        IReadOnlyCollection<string> requiredPermissions,
        string errorCode,
        string message,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new BusinessRuleException(errorCode, message);
        }

        var access = await _accessControlService.GetEffectiveAccessAsync(
            _currentUserService.UserId.Value,
            cancellationToken);
        var actorPermissions = access.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (actorPermissions.Contains("*") ||
            requiredPermissions.Any(actorPermissions.Contains))
        {
            return;
        }

        throw new BusinessRuleException(errorCode, message);
    }

    private static bool IsAdministrativeAccessPermission(string permission) =>
        string.Equals(permission, PermissionKeys.Admin.UsersAccessView, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessCreate, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessEdit, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessApprove, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(permission, PermissionKeys.Admin.UsersAccessManageSettings, StringComparison.OrdinalIgnoreCase);
}
