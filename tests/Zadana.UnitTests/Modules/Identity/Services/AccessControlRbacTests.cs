using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Zadana.Application.Modules.Identity.Commands.UpdateRole;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Modules.Identity.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Identity.Services;

public class AccessControlRbacTests
{
    [Fact]
    public async Task GetEffectiveAccessAsync_WhenUserIsSuspended_ReturnsNoPermissions()
    {
        using var context = TestDbContextFactory.Create();
        var permission = AddPermission(context, PermissionKeys.Admin.DashboardView);
        var role = AddRole(context, "ops", UserRole.Admin, PanelScope.SuperAdminPanel, [permission]);
        var user = new User("Suspended User", "suspended@example.com", "01000000001", UserRole.Admin);
        user.Suspend();

        context.Users.Add(user);
        context.UserAccessScopes.Add(new UserAccessScope(user.Id, role.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global));
        await context.SaveChangesAsync();

        var service = CreateAccessControlService(context);

        var result = await service.GetEffectiveAccessAsync(user.Id);

        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureCanMutateUserAccessAsync_WhenAssigningSensitiveRoleWithoutApproval_Throws()
    {
        using var context = TestDbContextFactory.Create();
        var actorEditPermission = AddPermission(context, PermissionKeys.Admin.UsersAccessEdit, isSensitive: true);
        var actorCreatePermission = AddPermission(context, PermissionKeys.Admin.UsersAccessCreate, isSensitive: true);
        var actorRole = AddRole(
            context,
            "access_editor",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [actorEditPermission, actorCreatePermission]);

        var financeApprovePermission = AddPermission(context, PermissionKeys.Admin.FinancesApprove, isSensitive: true);
        var sensitiveRole = AddRole(
            context,
            "finance_approver",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [financeApprovePermission]);

        var actor = new User("Access Editor", "access-editor@example.com", "01000000002", UserRole.Admin);
        var target = new User("Target User", "target@example.com", "01000000003", UserRole.Admin);
        context.Users.AddRange(actor, target);
        context.UserAccessScopes.Add(new UserAccessScope(actor.Id, actorRole.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global));
        await context.SaveChangesAsync();

        var validator = CreateValidationService(context, actor.Id);

        var act = () => validator.EnsureCanMutateUserAccessAsync(
            target.Id,
            target.Role,
            requestedStatus: null,
            actor.Id,
            sensitiveRole,
            grantedPermissions: [],
            revokedPermissions: [],
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "ACCESS_APPROVAL_REQUIRED");
        context.AccessApprovalRequests.Should().ContainSingle(request =>
            request.Status == AccessApprovalStatus.Pending &&
            request.TargetUserId == target.Id &&
            request.Action == "user-access-change");
    }

    [Fact]
    public async Task EnsureCanMutateUserAccessAsync_WhenActorRemovesOwnAccessManagement_Throws()
    {
        using var context = TestDbContextFactory.Create();
        var accessEditPermission = AddPermission(context, PermissionKeys.Admin.UsersAccessEdit, isSensitive: true);
        var currentRole = AddRole(
            context,
            "access_manager",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [accessEditPermission]);

        var dashboardPermission = AddPermission(context, PermissionKeys.Admin.DashboardView);
        var restrictedRole = AddRole(
            context,
            "dashboard_viewer",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [dashboardPermission]);

        var actor = new User("Access Manager", "self-access@example.com", "01000000004", UserRole.Admin);
        context.Users.Add(actor);
        context.UserAccessScopes.Add(new UserAccessScope(actor.Id, currentRole.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global));
        await context.SaveChangesAsync();

        var validator = CreateValidationService(context, actor.Id);

        var act = () => validator.EnsureCanMutateUserAccessAsync(
            actor.Id,
            actor.Role,
            requestedStatus: null,
            actor.Id,
            restrictedRole,
            grantedPermissions: [],
            revokedPermissions: [],
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BadRequestException>()
            .Where(exception => exception.ErrorCode == "SELF_ACCESS_CHANGE_BLOCKED");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenExistingRoleHasSensitivePermissionWithoutManageSettings_Throws()
    {
        using var context = TestDbContextFactory.Create();
        var actorEditPermission = AddPermission(context, PermissionKeys.Admin.UsersAccessEdit, isSensitive: true);
        var actorRole = AddRole(
            context,
            "access_editor_for_roles",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [actorEditPermission]);

        var sensitivePermission = AddPermission(context, PermissionKeys.Admin.FinancesApprove, isSensitive: true);
        var dashboardPermission = AddPermission(context, PermissionKeys.Admin.DashboardView);
        var sensitiveRole = AddRole(
            context,
            "finance_sensitive_role",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [sensitivePermission]);

        var actor = new User("Role Editor", "role-editor@example.com", "01000000005", UserRole.Admin);
        context.Users.Add(actor);
        context.UserAccessScopes.Add(new UserAccessScope(actor.Id, actorRole.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global));
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(
            context,
            CreateValidationService(context, actor.Id));

        var command = new UpdateRoleCommand(
            sensitiveRole.Id,
            "Finance Viewer",
            null,
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [dashboardPermission.Key]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "ACCESS_APPROVAL_REQUIRED");
        context.AccessApprovalRequests.Should().ContainSingle(request =>
            request.Status == AccessApprovalStatus.Pending &&
            request.Action == "role-definition-change");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenConvertingUnassignedSuperAdminRoleWithManageSettings_Succeeds()
    {
        using var context = TestDbContextFactory.Create();
        var manageSettingsPermission = AddPermission(context, PermissionKeys.Admin.UsersAccessManageSettings, isSensitive: true);
        var actorRole = AddRole(
            context,
            "access_settings_manager",
            UserRole.Admin,
            PanelScope.SuperAdminPanel,
            [manageSettingsPermission]);

        var sensitivePermission = AddPermission(context, PermissionKeys.Admin.FinancesApprove, isSensitive: true);
        var vendorPermission = AddPermission(
            context,
            PermissionKeys.Vendor.ProfileView,
            PanelScope.VendorPanel);
        var superAdminRole = AddRole(
            context,
            "unassigned_super_admin_role",
            UserRole.SuperAdmin,
            PanelScope.SuperAdminPanel,
            [sensitivePermission]);

        var actor = new User("Settings Manager", "settings-manager@example.com", "01000000006", UserRole.Admin);
        context.Users.Add(actor);
        context.UserAccessScopes.Add(new UserAccessScope(actor.Id, actorRole.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global));
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(
            context,
            CreateValidationService(context, actor.Id));

        var result = await handler.Handle(
            new UpdateRoleCommand(
                superAdminRole.Id,
                "Vendor Staff Role",
                null,
                UserRole.VendorStaff,
                PanelScope.VendorPanel,
                [vendorPermission.Key]),
            CancellationToken.None);

        result.IdentityRole.Should().Be(UserRole.VendorStaff);
        result.PanelScope.Should().Be(PanelScope.VendorPanel);
    }

    private static PermissionDefinition AddPermission(
        ApplicationDbContext context,
        string key,
        PanelScope panelScope = PanelScope.SuperAdminPanel,
        bool isSensitive = false)
    {
        var parts = key.Split('.', 2);
        var permission = new PermissionDefinition(
            key,
            key,
            parts[0],
            parts.Length > 1 ? parts[1] : "manage",
            panelScope,
            isSensitive: isSensitive);
        context.PermissionDefinitions.Add(permission);
        return permission;
    }

    private static RoleDefinition AddRole(
        ApplicationDbContext context,
        string code,
        UserRole identityRole,
        PanelScope panelScope,
        IReadOnlyCollection<PermissionDefinition> permissions)
    {
        var role = new RoleDefinition(code, code, identityRole, panelScope, isSystem: false);
        context.RoleDefinitions.Add(role);

        foreach (var permission in permissions)
        {
            context.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
        }

        return role;
    }

    private static AccessControlService CreateAccessControlService(ApplicationDbContext context) =>
        new(context, NullLogger<AccessControlService>.Instance);

    private static AdminAccessValidationService CreateValidationService(
        ApplicationDbContext context,
        Guid actorUserId)
    {
        var currentUser = new FakeCurrentUserService(actorUserId, isAuthenticated: true, role: UserRole.Admin.ToString());
        return new AdminAccessValidationService(
            context,
            currentUser,
            CreateAccessControlService(context));
    }
}
