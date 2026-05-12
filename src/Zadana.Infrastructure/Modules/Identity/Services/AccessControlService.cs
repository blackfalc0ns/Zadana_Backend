using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public class AccessControlService : IAccessControlService
{
    private readonly IApplicationDbContext _context;

    public AccessControlService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EffectiveAccessDto> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return new EffectiveAccessDto(0, []);
        }

        var activeScope = await _context.UserAccessScopes
            .AsNoTracking()
            .Include(x => x.RoleDefinition)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.PermissionDefinition)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
        var fallbackRole = activeScope is null
            ? await _context.RoleDefinitions
                .AsNoTracking()
                .Include(x => x.RolePermissions)
                .ThenInclude(x => x.PermissionDefinition)
                .Where(x => x.IdentityRole == user.Role && x.IsActive)
                .OrderByDescending(x => x.IsSystem)
                .ThenBy(x => x.Code)
                .FirstOrDefaultAsync(x => x.Code == ResolvePreferredRoleCode(user.Role), cancellationToken)
            : null;
        fallbackRole ??= activeScope is null
            ? await _context.RoleDefinitions
                .AsNoTracking()
                .Include(x => x.RolePermissions)
                .ThenInclude(x => x.PermissionDefinition)
                .Where(x => x.IdentityRole == user.Role && x.IsActive)
                .OrderByDescending(x => x.IsSystem)
                .ThenBy(x => x.Code)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var role = activeScope?.RoleDefinition ?? fallbackRole;
        var panelScope = activeScope?.PanelScope ?? role?.PanelScope ?? PanelScope.SuperAdminPanel;

        var permissions = role?.RolePermissions
            .Select(x => x.PermissionDefinition.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var validOverrideKeys = await _context.PermissionDefinitions
            .AsNoTracking()
            .Where(x => x.PanelScope == panelScope)
            .Select(x => x.Key)
            .ToListAsync(cancellationToken);
        var validOverrideSet = validOverrideKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var overrides = await _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && validOverrideSet.Contains(x.PermissionKey))
            .ToListAsync(cancellationToken);

        foreach (var overrideEntry in overrides)
        {
            if (overrideEntry.Mode == PermissionOverrideMode.Grant)
            {
                permissions.Add(overrideEntry.PermissionKey);
                continue;
            }

            permissions.Remove(overrideEntry.PermissionKey);
        }

        var scopeDto = new AccessScopeDto(
            panelScope.ToString(),
            (activeScope?.ScopeType ?? ResolveDefaultScopeType(panelScope)).ToString(),
            activeScope?.ScopeEntityId,
            role?.Code ?? ResolvePreferredRoleCode(user.Role),
            role?.Name ?? user.Role.ToString());

        return new EffectiveAccessDto(
            user.PermissionVersion,
            permissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            scopeDto);
    }

    private static string ResolvePreferredRoleCode(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "super_admin_all",
        UserRole.Admin => "admin_operations",
        UserRole.Vendor => "vendor_owner",
        UserRole.VendorStaff => "vendor_company_manager",
        UserRole.Driver => "driver_account",
        UserRole.Customer => "customer_account",
        _ => "admin_operations"
    };

    private static AccessScopeType ResolveDefaultScopeType(PanelScope panelScope) => panelScope switch
    {
        PanelScope.VendorPanel => AccessScopeType.VendorCompany,
        PanelScope.DriverApp => AccessScopeType.DriverSelf,
        PanelScope.CustomerApp => AccessScopeType.CustomerSelf,
        _ => AccessScopeType.Global
    };
}
