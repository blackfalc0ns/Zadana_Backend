using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Queries.GetAdminUsers;

public record GetAdminUsersQuery : IRequest<List<AdminUserRecordDto>>;
public record GetAdminUserByIdQuery(Guid Id) : IRequest<AdminUserRecordDto?>;

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, List<AdminUserRecordDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AdminUserRecordDto>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        return await AdminUserRecordProjector.BuildAsync(
            _context,
            _context.Users.OrderBy(u => u.FullName),
            cancellationToken);
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

internal static class AdminUserRecordProjector
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

        var roleIds = scopes
            .Select(s => s.RoleDefinitionId)
            .Distinct()
            .ToList();

        var roles = await context.RoleDefinitions
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        return users
            .Select(user => BuildRecord(user, scopes, roles))
            .ToList();
    }

    private static AdminUserRecordDto BuildRecord(
        User user,
        List<UserAccessScope> scopes,
        List<RoleDefinition> roles)
    {
        var primaryScope = scopes.FirstOrDefault(s => s.UserId == user.Id);
        var role = roles.FirstOrDefault(r => r.Id == primaryScope?.RoleDefinitionId);

        var panelScope = primaryScope?.PanelScope switch
        {
            PanelScope.VendorPanel => "vendor_panel",
            PanelScope.DriverApp => "driver_app",
            PanelScope.CustomerApp => "customer_app",
            _ => "super_admin_panel"
        };

        var (personaType, audienceType, identityKind) = ResolveAudience(primaryScope);
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
            Department: "Operations",
            Team: "Core",
            PersonaType: personaType,
            AudienceType: audienceType,
            IdentityKind: identityKind,
            PanelScope: panelScope,
            RolePresetId: rolePresetId,
            AccessLevel: accessLevel,
            Status: status,
            InviteState: user.EmailConfirmed ? "accepted" : "pending",
            GrantedPermissions: [],
            RevokedPermissions: [],
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

    private static (string PersonaType, string AudienceType, string IdentityKind) ResolveAudience(UserAccessScope? primaryScope)
    {
        if (primaryScope is null)
        {
            return ("super_admin_staff", "super_admin", "operational");
        }

        return primaryScope.PanelScope switch
        {
            PanelScope.VendorPanel when primaryScope.ScopeType == AccessScopeType.VendorBranch
                => ("vendor_branch_employee", "vendor_network", "operational"),
            PanelScope.VendorPanel
                => ("vendor_company_manager", "vendor_network", "operational"),
            PanelScope.DriverApp
                => ("driver", "drivers", "external"),
            PanelScope.CustomerApp
                => ("customer", "customers", "external"),
            _ => ("super_admin_staff", "super_admin", "operational")
        };
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
}
