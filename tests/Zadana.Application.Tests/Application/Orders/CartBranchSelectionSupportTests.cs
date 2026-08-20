using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class CartBranchSelectionSupportTests
{
    [Fact]
    public async Task ResolveAddressBranchIdsByVendorAsync_WhenCityOnlyAddress_ShouldNotBindPrimaryBranch()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("City Only Customer", "cart.cityonly.customer@test.com", "01000000300", UserRole.Customer);
        var vendorUser = new User("City Only Vendor", "cart.cityonly.vendor@test.com", "01000000301", UserRole.Vendor);
        var vendor = new Vendor(
            vendorUser.Id,
            "متجر",
            "City Only Store",
            "Groceries",
            "1234567866",
            "cart.cityonly.vendor@test.com",
            "01000000301",
            city: "Dammam");
        vendor.Approve(10m, Guid.NewGuid());

        var primaryBranch = new VendorBranch(
            vendor.Id,
            "Primary Dammam",
            "BR-PRIMARY",
            isPrimary: true,
            "Primary Road",
            "EASTERN",
            "DAMMAM",
            latitude: 26.43m,
            longitude: 50.08m,
            contactPhone: "01000000302",
            managerName: "Manager",
            managerContact: "01000000303",
            deliveryRadiusKm: 50m);
        var address = new CustomerAddress(
            customer.Id,
            "City Only Customer",
            "01000000300",
            "No GPS Street",
            AddressLabel.Home,
            city: "Dammam");

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(primaryBranch);
        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync();

        var branchIds = await CartBranchSelectionSupport.ResolveAddressBranchIdsByVendorAsync(
            dbContext,
            [vendor.Id],
            address,
            CancellationToken.None);

        branchIds.Should().ContainKey(vendor.Id);
        branchIds[vendor.Id].Should().BeNull();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
