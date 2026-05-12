using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Queries.GetAdminUsers;

public record GetAdminUsersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? RoleDefinitionId = null,
    PanelScope? PanelScope = null) : IRequest<PagedResultDto<AdminUserRecordDto>>;
public record GetAdminUserByIdQuery(Guid Id) : IRequest<AdminUserRecordDto?>;

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, PagedResultDto<AdminUserRecordDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<AdminUserRecordDto>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            query = normalizedStatus switch
            {
                "active" => query.Where(u => u.AccountStatus == AccountStatus.Active),
                "suspended" => query.Where(u => u.AccountStatus == AccountStatus.Suspended || u.AccountStatus == AccountStatus.Banned),
                "inactive" => query.Where(u => u.AccountStatus == AccountStatus.Inactive),
                _ => query
            };
        }

        if (request.RoleDefinitionId.HasValue || request.PanelScope.HasValue)
        {
            var scopeQuery = _context.UserAccessScopes.Where(s => s.IsActive);
            if (request.RoleDefinitionId.HasValue)
            {
                scopeQuery = scopeQuery.Where(s => s.RoleDefinitionId == request.RoleDefinitionId.Value);
            }
            if (request.PanelScope.HasValue)
            {
                scopeQuery = scopeQuery.Where(s => s.PanelScope == request.PanelScope.Value);
            }

            query = query.Where(u => scopeQuery.Any(s => s.UserId == u.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = query
            .OrderBy(u => u.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var items = await AdminUserRecordProjector.BuildAsync(
            _context,
            users,
            cancellationToken);

        return new PagedResultDto<AdminUserRecordDto>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}

public class GetAdminUserByIdQueryHandler : IRequestHandler<GetAdminUserByIdQuery, AdminUserRecordDto?>
{
    private readonly IApplicationDbContext _context;

    public GetAdminUserByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminUserRecordDto?> Handle(GetAdminUserByIdQuery request, CancellationToken cancellationToken)
    {
        var users = await AdminUserRecordProjector.BuildAsync(
            _context,
            _context.Users.Where(u => u.Id == request.Id),
            cancellationToken);

        return users.FirstOrDefault();
    }
}

public static class AdminUserRecordProjector
{
    private static readonly string[] Accents = ["#0891b2", "#7c3aed", "#dc2626", "#059669", "#d97706", "#4f46e5"];

    public static async Task<List<AdminUserRecordDto>> BuildAsync(
        IApplicationDbContext context,
        IQueryable<User> userQuery,
        CancellationToken cancellationToken)
    {
        var users = await userQuery.ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(u => u.Id).ToList();
        var scopes = await context.UserAccessScopes
            .Where(s => userIds.Contains(s.UserId) && s.IsActive)
            .ToListAsync(cancellationToken);
        var overrides = await context.UserPermissionOverrides
            .Where(o => userIds.Contains(o.UserId) && o.IsActive)
            .ToListAsync(cancellationToken);

        var roleIds = scopes
            .Select(s => s.RoleDefinitionId)
            .Distinct()
            .ToList();
        var identityRoles = users
            .Select(u => u.Role)
            .Distinct()
            .ToList();

        var roles = await context.RoleDefinitions
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.PermissionDefinition)
            .Where(r => roleIds.Contains(r.Id) || identityRoles.Contains(r.IdentityRole))
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);
        var permissions = await context.PermissionDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return users
            .Select(user => BuildRecord(user, scopes, roles, overrides, permissions))
            .ToList();
    }

    private static AdminUserRecordDto BuildRecord(
        User user,
        List<UserAccessScope> scopes,
        List<RoleDefinition> roles,
        List<UserPermissionOverride> overrides,
        List<PermissionDefinition> permissions)
    {
        var primaryScope = scopes
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .ThenByDescending(s => s.GrantedAtUtc)
            .FirstOrDefault();
        var role = roles.FirstOrDefault(r => r.Id == primaryScope?.RoleDefinitionId)
            ?? ResolveFallbackRole(user.Role, roles);
        var rolePermissions = role?.RolePermissions
            .Select(rp => rp.PermissionDefinition.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        var rolePermissionSet = rolePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var effectivePanelScope = primaryScope?.PanelScope ?? role?.PanelScope ?? PanelScope.SuperAdminPanel;
        var validOverrideKeys = permissions
            .Where(p => p.PanelScope == effectivePanelScope)
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var grantedPermissions = overrides
            .Where(o => o.UserId == user.Id && o.Mode == PermissionOverrideMode.Grant)
            .Select(o => o.PermissionKey)
            .Where(validOverrideKeys.Contains)
            .Where(permission => !rolePermissionSet.Contains(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var revokedPermissions = overrides
            .Where(o => o.UserId == user.Id && o.Mode == PermissionOverrideMode.Revoke)
            .Select(o => o.PermissionKey)
            .Where(validOverrideKeys.Contains)
            .Where(rolePermissionSet.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var panelScope = effectivePanelScope switch
        {
            PanelScope.VendorPanel => "vendor_panel",
            PanelScope.DriverApp => "driver_app",
            PanelScope.CustomerApp => "customer_app",
            _ => "super_admin_panel"
        };

        var (personaType, audienceType, identityKind) = ResolveAudience(primaryScope, role, user.Role);
        var rolePresetId = NormalizeRolePresetId(role?.Code, panelScope, personaType);
        var accessLevel = ResolveAccessLevel(rolePresetId);
        var status = user.AccountStatus switch
        {
            AccountStatus.Active => "active",
            AccountStatus.Suspended => "suspended",
            AccountStatus.Banned => "suspended",
            AccountStatus.Inactive => "inactive",
            _ => "active"
        };

        var colorIndex = Math.Abs(user.Id.GetHashCode()) % Accents.Length;

        return new AdminUserRecordDto(
            Id: user.Id,
            EntityId: primaryScope?.ScopeEntityId?.ToString(),
            Source: ResolveSource(panelScope),
            FullName: user.FullName,
            Email: user.Email ?? "",
            Phone: user.PhoneNumber ?? "",
            Department: user.Department ?? "",
            Team: user.Team ?? "",
            PersonaType: personaType,
            AudienceType: audienceType,
            IdentityKind: identityKind,
            PanelScope: panelScope,
            RoleDefinitionId: role?.Id,
            RoleCode: role?.Code ?? rolePresetId,
            RoleName: role?.Name ?? ResolveRoleLabel(rolePresetId),
            RolePermissions: rolePermissions,
            RolePresetId: rolePresetId,
            AccessLevel: accessLevel,
            Status: status,
            InviteState: user.EmailConfirmed ? "accepted" : "pending",
            MustChangePassword: user.MustChangePassword,
            GrantedPermissions: grantedPermissions,
            RevokedPermissions: revokedPermissions,
            Security: new AdminUserSecurityDto(
                MfaEnabled: false,
                LastLoginAt: user.LastLoginAtUtc?.ToString("o"),
                InvitedBy: "System",
                InvitedAt: user.CreatedAtUtc.ToString("o"),
                AcceptedAt: user.EmailConfirmed ? user.CreatedAtUtc.ToString("o") : null,
                VerificationState: identityKind == "external" && !user.EmailConfirmed ? "pending" : "verified"
            ),
            AvatarHue: Accents[colorIndex],
            Assignment: new DirectoryAssignmentDto(
                EntityId: primaryScope?.ScopeEntityId?.ToString(),
                EntitySource: ResolveSource(panelScope),
                VendorId: primaryScope?.ScopeType == AccessScopeType.VendorCompany ? primaryScope.ScopeEntityId?.ToString() : null,
                VendorName: "",
                BranchId: primaryScope?.ScopeType == AccessScopeType.VendorBranch ? primaryScope.ScopeEntityId?.ToString() : null,
                BranchName: "",
                Region: "",
                City: ""
            ),
            Communication: new DirectoryCommunicationProfileDto(
                PrimaryEmail: user.Email ?? "",
                NotificationEmails: [],
                ReplyTo: user.Email ?? "",
                EscalationEmails: [],
                PreferredLocale: "ar",
                EmailOptIn: new { }
            ),
            FeatureToggles: [],
            EntityPath: $"/admin-users/{user.Id}",
            Tags: []
        );
    }

    private static (string PersonaType, string AudienceType, string IdentityKind) ResolveAudience(
        UserAccessScope? primaryScope,
        RoleDefinition? role,
        UserRole userRole)
    {
        var panelScope = primaryScope?.PanelScope ?? role?.PanelScope ?? PanelScope.SuperAdminPanel;
        var scopeType = primaryScope?.ScopeType;

        if (primaryScope is null && role is null)
        {
            return userRole switch
            {
                UserRole.SuperAdmin => ("super_admin_manager", "super_admin", "operational"),
                UserRole.Admin => ("super_admin_staff", "super_admin", "operational"),
                UserRole.Vendor => ("vendor_owner", "vendor_network", "operational"),
                UserRole.VendorStaff => ("vendor_company_manager", "vendor_network", "operational"),
                UserRole.Driver => ("driver", "drivers", "external"),
                UserRole.Customer => ("customer", "customers", "external"),
                _ => ("super_admin_staff", "super_admin", "operational")
            };
        }

        return panelScope switch
        {
            PanelScope.VendorPanel when scopeType == AccessScopeType.VendorBranch
                => ("vendor_branch_employee", "vendor_network", "operational"),
            PanelScope.VendorPanel when userRole == UserRole.Vendor
                => ("vendor_owner", "vendor_network", "operational"),
            PanelScope.VendorPanel
                => ("vendor_company_manager", "vendor_network", "operational"),
            PanelScope.DriverApp
                => ("driver", "drivers", "external"),
            PanelScope.CustomerApp
                => ("customer", "customers", "external"),
            _ => ("super_admin_staff", "super_admin", "operational")
        };
    }

    private static RoleDefinition? ResolveFallbackRole(UserRole userRole, IReadOnlyCollection<RoleDefinition> roles)
    {
        var preferredCode = userRole switch
        {
            UserRole.SuperAdmin => "super_admin_all",
            UserRole.Admin => "admin_operations",
            UserRole.Vendor => "vendor_owner",
            UserRole.VendorStaff => "vendor_company_manager",
            UserRole.Driver => "driver_account",
            UserRole.Customer => "customer_account",
            _ => "admin_operations"
        };

        return roles.FirstOrDefault(r => r.Code == preferredCode)
            ?? roles.FirstOrDefault(r => r.IdentityRole == userRole && r.IsSystem)
            ?? roles.FirstOrDefault(r => r.IdentityRole == userRole);
    }

    private static string NormalizeRolePresetId(string? roleCode, string panelScope, string personaType)
    {
        return roleCode switch
        {
            "super_admin_all" => "super_admin",
            "admin_operations" => "operations_lead",
            "vendor_branch_staff" => "vendor_branch_employee",
            "vendor_owner" => "vendor_owner",
            "vendor_branch_manager" => "vendor_branch_manager",
            "driver_account" => "driver_account",
            "customer_account" => "customer_account",
            _ when panelScope == "vendor_panel" && personaType == "vendor_company_manager" => "vendor_company_manager",
            _ when panelScope == "vendor_panel" => "vendor_branch_employee",
            _ when panelScope == "driver_app" => "driver_account",
            _ when panelScope == "customer_app" => "customer_account",
            _ => "operations_lead"
        };
    }

    private static string ResolveAccessLevel(string rolePresetId)
    {
        return rolePresetId switch
        {
            "super_admin" => "full",
            "vendor_owner" => "full",
            "support_admin" => "observer",
            "vendor_branch_employee" => "observer",
            "customer_account" => "observer",
            _ => "restricted"
        };
    }

    private static string ResolveSource(string panelScope)
    {
        return panelScope switch
        {
            "vendor_panel" => "vendor",
            "driver_app" => "driver",
            "customer_app" => "customer",
            _ => "admin"
        };
    }

    private static string ResolveRoleLabel(string rolePresetId)
    {
        return rolePresetId switch
        {
            "super_admin" => "Super Admin",
            "vendor_owner" => "Vendor Owner",
            "vendor_company_manager" => "Vendor Company Manager",
            "vendor_branch_manager" => "Vendor Branch Manager",
            "driver_account" => "Driver Account",
            "customer_account" => "Customer Account",
            _ => "Operations Lead"
        };
    }
}
