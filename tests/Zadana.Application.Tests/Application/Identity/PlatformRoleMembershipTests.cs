using FluentAssertions;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Application.Identity;

public class PlatformRoleMembershipTests
{
    [Fact]
    public void ResolveSessionRole_WhenUserHasRequestedAppRole_ReturnsThatRole()
    {
        var user = BuildUser(UserRole.Vendor, [UserRole.Vendor, UserRole.Customer]);

        var session = PlatformRoleMembership.ResolveSessionRole(user, [UserRole.Customer]);

        session.Should().Be(UserRole.Customer);
    }

    [Fact]
    public void ResolveSessionRole_WhenUserDoesNotHaveAppRole_ReturnsNull()
    {
        var user = BuildUser(UserRole.Vendor, [UserRole.Vendor]);

        var session = PlatformRoleMembership.ResolveSessionRole(user, [UserRole.Customer]);

        session.Should().BeNull();
    }

    [Fact]
    public void OccupyingRoles_ForVendor_IncludesStaff()
    {
        PlatformRoleMembership.OccupyingRoles(UserRole.Vendor)
            .Should()
            .BeEquivalentTo([UserRole.Vendor, UserRole.VendorStaff]);
    }

    private static IdentityAccountSnapshot BuildUser(UserRole primary, UserRole[] platformRoles) =>
        new(
            Guid.NewGuid(),
            "Test User",
            "user@test.com",
            "01000000000",
            primary,
            1,
            AccountStatus.Active,
            false,
            null,
            null,
            true,
            true,
            false,
            null,
            platformRoles);
}
