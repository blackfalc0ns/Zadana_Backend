using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
namespace Zadana.Infrastructure.Modules.Identity.Services;

public class AccessControlService : IAccessControlService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AccessControlService> _logger;

    public AccessControlService(IApplicationDbContext context, ILogger<AccessControlService> logger)
    {
        _context = context;
        _logger = logger;
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

        if (user.AccountStatus != AccountStatus.Active || user.IsLoginLocked)
        {
            _logger.LogWarning(
                "Access denied while resolving effective access for inactive or locked user {UserId}. Status={AccountStatus}, IsLoginLocked={IsLoginLocked}",
                userId,
                user.AccountStatus,
                user.IsLoginLocked);

            return new EffectiveAccessDto(user.PermissionVersion, []);
        }

        var activeScope = await _context.UserAccessScopes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);

        RoleDefinition? activeRole = null;
        if (activeScope is not null)
        {
            activeRole = await _context.RoleDefinitions
                .AsNoTracking()
                .Include(x => x.RolePermissions)
                .ThenInclude(x => x.PermissionDefinition)
                .FirstOrDefaultAsync(x => x.Id == activeScope.RoleDefinitionId && x.IsActive, cancellationToken);
        }

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
        var role = activeRole ?? fallbackRole;
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

        ApplySessionBaselinePermissions(user.Role, panelScope, permissions);

        var scopeType = activeScope?.ScopeType ?? ResolveDefaultScopeType(panelScope);
        var (scopeEntityName, scopeClassification) = await ResolveScopePresentationAsync(
            userId,
            panelScope,
            scopeType,
            activeScope?.ScopeEntityId,
            cancellationToken);

        var scopeDto = new AccessScopeDto(
            panelScope.ToString(),
            scopeType.ToString(),
            activeScope?.ScopeEntityId,
            role?.Code ?? ResolvePreferredRoleCode(user.Role),
            role?.Name ?? user.Role.ToString(),
            scopeEntityName,
            scopeClassification);

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

    private static void ApplySessionBaselinePermissions(
        UserRole role,
        PanelScope panelScope,
        ISet<string> permissions)
    {
        if (panelScope != PanelScope.VendorPanel ||
            (role != UserRole.Vendor && role != UserRole.VendorStaff))
        {
            return;
        }

        foreach (var permission in PermissionKeys.Vendor.SessionBaseline)
        {
            permissions.Add(permission);
        }
    }

    private static AccessScopeType ResolveDefaultScopeType(PanelScope panelScope) => panelScope switch
    {
        PanelScope.VendorPanel => AccessScopeType.VendorCompany,
        PanelScope.DriverApp => AccessScopeType.DriverSelf,
        PanelScope.CustomerApp => AccessScopeType.CustomerSelf,
        _ => AccessScopeType.Global
    };

    private async Task<(string? ScopeEntityName, string? ScopeClassification)> ResolveScopePresentationAsync(
        Guid userId,
        PanelScope panelScope,
        AccessScopeType scopeType,
        Guid? scopeEntityId,
        CancellationToken cancellationToken)
    {
        if (panelScope != PanelScope.VendorPanel)
        {
            return (null, null);
        }

        if (scopeType == AccessScopeType.VendorBranch && scopeEntityId.HasValue)
        {
            var branch = await _context.VendorBranches
                .AsNoTracking()
                .Where(item => item.Id == scopeEntityId.Value)
                .Select(item => new { item.Name })
                .FirstOrDefaultAsync(cancellationToken);

            return (branch?.Name, "branch");
        }

        if (scopeType == AccessScopeType.VendorCompany)
        {
            var vendor = await _context.Vendors
                .AsNoTracking()
                .Where(item => item.UserId == userId || (scopeEntityId.HasValue && item.Id == scopeEntityId.Value))
                .Select(item => new
                {
                    item.BusinessNameAr,
                    item.BusinessNameEn
                })
                .FirstOrDefaultAsync(cancellationToken);

            var scopeName = vendor?.BusinessNameAr
                ?? vendor?.BusinessNameEn;

            return (scopeName, "primary");
        }

        return (null, null);
    }
}
