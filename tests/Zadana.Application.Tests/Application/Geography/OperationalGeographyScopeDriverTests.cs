using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Geography;

public class OperationalGeographyScopeDriverTests
{
    [Fact]
    public async Task EnsureDriverServiceAreaAsync_WhenEasternAndVendorInKhobar_ShouldAllowDammamOmittedCity()
    {
        await using var dbContext = CreateDbContext();

        var vendorUser = new User(
            "Active Vendor",
            $"active-vendor-{Guid.NewGuid():N}@example.com",
            "+201009998877",
            UserRole.Vendor);

        var vendor = new Vendor(
            vendorUser.Id,
            "Active Vendor",
            "Active Vendor",
            "Grocery",
            $"CR{Guid.NewGuid():N}"[..12],
            "active-vendor@example.com",
            "+201009998877",
            region: "EASTERN",
            city: "KHOBAR");

        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(
            vendor.Id,
            "Main Branch",
            "MAIN",
            true,
            "Khobar branch address",
            "EASTERN",
            "KHOBAR",
            26.2m,
            50.2m,
            "+201009998877",
            "Manager",
            "+201009998877",
            5m);

        dbContext.Users.Add(vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        await dbContext.SaveChangesAsync();

        var action = () => OperationalGeographyScope.EnsureDriverServiceAreaAsync(
            dbContext,
            "EASTERN",
            CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
