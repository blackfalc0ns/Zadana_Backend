using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Checkout;

public class CheckoutSupportTests
{
    [Fact]
    public async Task EvaluateDeliveryAsync_WhenCustomerWithinFiftyKmButOutsideBranchDeliveryRadius_ShouldRemainDeliverable()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Eastern Customer", "checkout.eastern.customer@test.com", "01000000200", UserRole.Customer);
        var vendorUser = new User("Eastern Vendor", "checkout.eastern.vendor@test.com", "01000000201", UserRole.Vendor);
        var vendor = new Vendor(
            vendorUser.Id,
            "متجر الشرقية",
            "Eastern Checkout Store",
            "Groceries",
            "1234567899",
            "checkout.eastern.vendor@test.com",
            "01000000201",
            city: "Dammam");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(
            vendor.Id,
            "Dammam Branch",
            "BR-DMM",
            isPrimary: true,
            "King Fahd Road",
            "EASTERN",
            "DAMMAM",
            latitude: 26.43m,
            longitude: 50.08m,
            contactPhone: "01000000202",
            managerName: "Branch Manager",
            managerContact: "01000000203",
            deliveryRadiusKm: 5m);
        var address = new CustomerAddress(
            customer.Id,
            "Eastern Customer",
            "01000000200",
            "Corniche",
            AddressLabel.Home,
            city: "Khobar",
            latitude: 26.2172m,
            longitude: 50.1971m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync();

        const decimal vendorToCustomerDistanceKm = 24.5m;
        vendorToCustomerDistanceKm.Should().BeLessThanOrEqualTo(DeliveryProximityLimits.MaxMatchKm);
        vendorToCustomerDistanceKm.Should().BeGreaterThan(branch.DeliveryRadiusKm);

        var deliveryPricingMock = new Mock<IDeliveryPricingService>();
        deliveryPricingMock
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(
                10m,
                5m,
                0m,
                15m,
                vendorToCustomerDistanceKm,
                "zone",
                "Eastern proximity",
                1m,
                vendorToCustomerDistanceKm,
                3m,
                12m,
                "driver",
                "vendor",
                false,
                "manual",
                null,
                "locked",
                DateTime.UtcNow,
                1,
                false));

        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(
            dbContext,
            deliveryPricingMock.Object,
            branch.Id,
            address,
            CancellationToken.None);

        assessment.DeliveryCheck.Status.Should().Be("deliverable");
        assessment.DeliveryCheck.IsDeliverable.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateDeliveryAsync_WhenCustomerBeyondFiftyKm_ShouldBeUndeliverable()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Far Customer", "checkout.far.customer@test.com", "01000000210", UserRole.Customer);
        var vendorUser = new User("Far Vendor", "checkout.far.vendor@test.com", "01000000211", UserRole.Vendor);
        var vendor = new Vendor(
            vendorUser.Id,
            "متجر بعيد",
            "Far Checkout Store",
            "Groceries",
            "1234567888",
            "checkout.far.vendor@test.com",
            "01000000211",
            city: "Khobar");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(
            vendor.Id,
            "Khobar Branch",
            "BR-KHB",
            isPrimary: true,
            "Corniche Road",
            "EASTERN",
            "KHOBAR",
            latitude: 26.22m,
            longitude: 50.19m,
            contactPhone: "01000000212",
            managerName: "Branch Manager",
            managerContact: "01000000213",
            deliveryRadiusKm: 100m);
        var address = new CustomerAddress(
            customer.Id,
            "Far Customer",
            "01000000210",
            "Olaya",
            AddressLabel.Home,
            city: "Riyadh",
            latitude: 24.71m,
            longitude: 46.67m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync();

        const decimal vendorToCustomerDistanceKm = 55m;
        vendorToCustomerDistanceKm.Should().BeGreaterThan(DeliveryProximityLimits.MaxMatchKm);

        var deliveryPricingMock = new Mock<IDeliveryPricingService>();
        deliveryPricingMock
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(
                10m,
                5m,
                0m,
                15m,
                vendorToCustomerDistanceKm,
                "zone",
                "Beyond platform radius",
                1m,
                vendorToCustomerDistanceKm,
                3m,
                12m,
                "driver",
                "vendor",
                false,
                "manual",
                null,
                "locked",
                DateTime.UtcNow,
                1,
                false));

        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(
            dbContext,
            deliveryPricingMock.Object,
            branch.Id,
            address,
            CancellationToken.None);

        assessment.DeliveryCheck.Status.Should().Be("undeliverable");
        assessment.DeliveryCheck.IsDeliverable.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateDeliveryAsync_WhenSameCityButBeyondFiftyKm_ShouldBeUndeliverable()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Same City Customer", "checkout.samecity.customer@test.com", "01000000220", UserRole.Customer);
        var vendorUser = new User("Same City Vendor", "checkout.samecity.vendor@test.com", "01000000221", UserRole.Vendor);
        var vendor = new Vendor(
            vendorUser.Id,
            "متجر",
            "Same City Store",
            "Groceries",
            "1234567877",
            "checkout.samecity.vendor@test.com",
            "01000000221",
            city: "Dammam");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(
            vendor.Id,
            "Dammam Branch",
            "BR-DMM-2",
            isPrimary: true,
            "King Fahd Road",
            "EASTERN",
            "DAMMAM",
            latitude: 26.43m,
            longitude: 50.08m,
            contactPhone: "01000000222",
            managerName: "Branch Manager",
            managerContact: "01000000223",
            deliveryRadiusKm: 100m);
        var address = new CustomerAddress(
            customer.Id,
            "Same City Customer",
            "01000000220",
            "Far District",
            AddressLabel.Home,
            city: "Dammam",
            latitude: 26.62m,
            longitude: 50.19m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync();

        const decimal vendorToCustomerDistanceKm = 52m;
        vendorToCustomerDistanceKm.Should().BeGreaterThan(DeliveryProximityLimits.MaxMatchKm);

        var deliveryPricingMock = new Mock<IDeliveryPricingService>();
        deliveryPricingMock
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(
                10m,
                5m,
                0m,
                15m,
                vendorToCustomerDistanceKm,
                "zone",
                "Same city beyond platform radius",
                1m,
                vendorToCustomerDistanceKm,
                3m,
                12m,
                "driver",
                "vendor",
                false,
                "manual",
                null,
                "locked",
                DateTime.UtcNow,
                1,
                false));

        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(
            dbContext,
            deliveryPricingMock.Object,
            branch.Id,
            address,
            CancellationToken.None);

        assessment.DeliveryCheck.Status.Should().Be("undeliverable");
        assessment.DeliveryCheck.IsDeliverable.Should().BeFalse();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
