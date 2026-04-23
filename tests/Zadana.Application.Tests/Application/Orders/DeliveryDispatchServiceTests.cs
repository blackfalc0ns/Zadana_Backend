using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DeliveryDispatchServiceTests
{
    [Fact]
    public async Task TryAutoDispatchAsync_ShouldPreferDriverWithFreshGpsInSameZone()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        var service = new DeliveryDispatchService(dbContext, dbContext, NullLogger<DeliveryDispatchService>.Instance);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
        decision.MatchReason.Should().Be("same-zone-live-gps");

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();
        assignment.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
        assignment.Status.Should().Be(AssignmentStatus.Accepted);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_ShouldPenalizeLowConfidenceGpsInsideSameZone()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext, lowConfidenceFreshDriver: true);
        var service = new DeliveryDispatchService(dbContext, dbContext, NullLogger<DeliveryDispatchService>.Instance);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SecondSameZoneDriver.Id);
        decision.MatchReason.Should().Be("same-zone-live-gps");
    }

    private static async Task<DispatchScenario> SeedDispatchScenarioAsync(
        ApplicationDbContext dbContext,
        bool lowConfidenceFreshDriver = false)
    {
        var customer = new User("Dispatch Customer", "dispatch.customer@test.com", "01000000992", UserRole.Customer);
        var vendorUser = new User("Dispatch Vendor", "dispatch.vendor@test.com", "01000000993", UserRole.Vendor);
        var sameZoneUser = new User("Fresh Zone Driver", "dispatch.driver.zone@test.com", "01000000994", UserRole.Driver);
        var secondZoneUser = new User("Fallback Zone Driver", "dispatch.driver.city@test.com", "01000000995", UserRole.Driver);
        var reserveZoneUser = new User("Second Same Zone Driver", "dispatch.driver.zone2@test.com", "01000000996", UserRole.Driver);

        var pickupZone = new DeliveryZone("Riyadh", "Al Olaya", 24.7136m, 46.6753m, 6m);
        var sameCityZone = new DeliveryZone("Riyadh", "Al Yasmin", 24.8296m, 46.6423m, 7m);

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر الاختبار",
            "Dispatch Store",
            "Groceries",
            "CR-100",
            "dispatch.vendor@test.com",
            "01000000993",
            region: "Riyadh",
            city: "Riyadh",
            nationalAddress: "Olaya");

        var branch = new VendorBranch(
            vendor.Id,
            "Olaya Branch",
            "King Fahd Rd",
            24.7137m,
            46.6754m,
            "01000000998",
            8m);

        var sameZoneFreshDriver = new Driver(sameZoneUser.Id, DriverVehicleType.Car, "1234567890", "DRV-ZONE-1");
        sameZoneFreshDriver.Approve(Guid.NewGuid());
        sameZoneFreshDriver.AssignZone(pickupZone.Id, pickupZone);
        sameZoneFreshDriver.ToggleAvailability(true);

        var sameCityFallbackDriver = new Driver(secondZoneUser.Id, DriverVehicleType.Car, "1234567891", "DRV-CITY-1");
        sameCityFallbackDriver.Approve(Guid.NewGuid());
        sameCityFallbackDriver.AssignZone(sameCityZone.Id, sameCityZone);
        sameCityFallbackDriver.ToggleAvailability(true);

        var secondSameZoneDriver = new Driver(reserveZoneUser.Id, DriverVehicleType.Car, "1234567892", "DRV-ZONE-2");
        secondSameZoneDriver.Approve(Guid.NewGuid());
        secondSameZoneDriver.AssignZone(pickupZone.Id, pickupZone);
        secondSameZoneDriver.ToggleAvailability(true);

        var order = new Order(
            "ORD-DISPATCH-001",
            customer.Id,
            vendor.Id,
            Guid.NewGuid(),
            PaymentMethodType.Card,
            120m,
            0m,
            15m,
            5m,
            vendorBranchId: branch.Id);
        order.ChangeStatus(OrderStatus.Placed);
        order.ChangeStatus(OrderStatus.Accepted);
        order.ChangeStatus(OrderStatus.Preparing);
        order.ChangeStatus(OrderStatus.ReadyForPickup);

        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Dispatch Product", 1, 120m));

        dbContext.Users.AddRange(customer, vendorUser, sameZoneUser, secondZoneUser, reserveZoneUser);
        dbContext.DeliveryZones.AddRange(pickupZone, sameCityZone);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.Drivers.AddRange(sameZoneFreshDriver, sameCityFallbackDriver, secondSameZoneDriver);
        dbContext.Orders.Add(order);

        dbContext.DriverLocations.Add(new DriverLocation(
            sameZoneFreshDriver.Id,
            24.7140m,
            46.6755m,
            lowConfidenceFreshDriver ? 150m : 12m));
        dbContext.DriverLocations.Add(new DriverLocation(
            secondSameZoneDriver.Id,
            24.7144m,
            46.6757m,
            10m));
        dbContext.DriverLocations.Add(new DriverLocation(
            sameCityFallbackDriver.Id,
            24.8301m,
            46.6420m,
            8m));

        await dbContext.SaveChangesAsync();

        return new DispatchScenario(order, sameZoneFreshDriver, sameCityFallbackDriver, secondSameZoneDriver);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private sealed record DispatchScenario(
        Order Order,
        Driver SameZoneFreshDriver,
        Driver SameCityFallbackDriver,
        Driver SecondSameZoneDriver);
}
