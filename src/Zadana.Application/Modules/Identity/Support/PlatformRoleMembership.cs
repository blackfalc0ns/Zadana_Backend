using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Support;

public static class PlatformRoleMembership
{
    public static UserRole[] OccupyingRoles(UserRole registeringAs) => registeringAs switch
    {
        UserRole.Vendor => [UserRole.Vendor, UserRole.VendorStaff],
        UserRole.Customer => [UserRole.Customer],
        UserRole.Driver => [UserRole.Driver],
        _ => [registeringAs]
    };

    public static bool IsSelfServePlatformRole(UserRole role) =>
        role is UserRole.Customer or UserRole.Vendor or UserRole.Driver;

    public static IReadOnlyCollection<UserRole> OwnedRoles(IdentityAccountSnapshot user)
    {
        var roles = new HashSet<UserRole> { user.Role };
        if (user.PlatformRoles is { Count: > 0 })
        {
            foreach (var role in user.PlatformRoles)
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    public static bool HasAnyRole(IdentityAccountSnapshot user, params UserRole[] expected)
    {
        if (expected is not { Length: > 0 })
        {
            return true;
        }

        var owned = OwnedRoles(user);
        return expected.Any(owned.Contains);
    }

    public static UserRole? ResolveSessionRole(IdentityAccountSnapshot user, UserRole[]? expectedRoles)
    {
        if (expectedRoles is not { Length: > 0 })
        {
            return user.Role;
        }

        var owned = OwnedRoles(user);
        foreach (var role in expectedRoles)
        {
            if (owned.Contains(role))
            {
                return role;
            }
        }

        return null;
    }

    public static IdentityAccountSnapshot WithSessionRole(IdentityAccountSnapshot user, UserRole sessionRole) =>
        user with { Role = sessionRole };

    public static PanelScope ToPanelScope(UserRole role) => role switch
    {
        UserRole.Customer => PanelScope.CustomerApp,
        UserRole.Vendor or UserRole.VendorStaff => PanelScope.VendorPanel,
        UserRole.Driver => PanelScope.DriverApp,
        _ => PanelScope.SuperAdminPanel
    };
}
