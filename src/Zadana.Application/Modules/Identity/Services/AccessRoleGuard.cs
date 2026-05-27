using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

internal static class AccessRoleGuard
{
    public static PanelScope ResolvePanelScope(UserRole identityRole) => identityRole switch
    {
        UserRole.SuperAdmin or UserRole.Admin => PanelScope.SuperAdminPanel,
        UserRole.Vendor or UserRole.VendorStaff => PanelScope.VendorPanel,
        UserRole.Driver => PanelScope.DriverApp,
        UserRole.Customer => PanelScope.CustomerApp,
        _ => PanelScope.SuperAdminPanel
    };

    public static void EnsureRoleMatchesPanelScope(UserRole identityRole, PanelScope panelScope)
    {
        var expectedPanelScope = ResolvePanelScope(identityRole);
        if (panelScope == expectedPanelScope)
        {
            return;
        }

        throw new BadRequestException(
            "ROLE_SCOPE_MISMATCH",
            $"The identity role {identityRole} can only be assigned to {expectedPanelScope}.");
    }

    public static void EnsureRoleCanBeAssignedToUser(User user, RoleDefinition role)
    {
        EnsureRoleMatchesPanelScope(role.IdentityRole, role.PanelScope);

        if (role.IdentityRole == user.Role)
        {
            return;
        }

        throw new BadRequestException(
            "ROLE_USER_MISMATCH",
            "The selected role definition does not match the user's identity role. Use the full access-user update workflow to change identity roles.");
    }
}
