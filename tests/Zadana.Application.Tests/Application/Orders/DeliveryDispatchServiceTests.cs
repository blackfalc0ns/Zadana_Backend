using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DeliveryDispatchServiceTests
{
    private static DeliveryDispatchService CreateDispatchService(
        ApplicationDbContext dbContext,
        IPublisher? publisher = null,
        INotificationService? notificationService = null,
        IOneSignalPushService? oneSignalPushService = null,
        IAdminAlertService? adminAlertService = null)
    {
        var commitmentPolicyService = new DriverCommitmentPolicyService(dbContext, dbContext);
        var pushServiceMock = new Mock<IOneSignalPushService>();
        pushServiceMock
            .Setup(service => service.SendMobileNotificationDirectAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "test-push",
                Reason: null));

        return new DeliveryDispatchService(
            dbContext,
            dbContext,
            NullLogger<DeliveryDispatchService>.Instance,
            publisher ?? Mock.Of<IPublisher>(),
            notificationService ?? Mock.Of<INotificationService>(),
            commitmentPolicyService,
            oneSignalPushService ?? pushServiceMock.Object,
            adminAlertService);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_ShouldPreferDriverWithFreshGpsInSameZone()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        var service = CreateDispatchService(dbContext);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
        decision.MatchReason.Should().Be("region-city-live-gps");

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();
        assignment.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
        assignment.Status.Should().Be(AssignmentStatus.OfferSent);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_ShouldUseBranchCityInsteadOfVendorCity()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(
            dbContext,
            vendorRegion: "Riyadh",
            vendorCity: "Riyadh",
            branchRegion: "Makkah",
            branchCity: "Jeddah",
            sameZoneDriverRegion: "MAKKAH",
            sameZoneDriverCity: "JEDDAH");
        var service = CreateDispatchService(dbContext);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();
        assignment.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_WhenNoDriverInBranchCity_ShouldNotOfferToOtherCities()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(
            dbContext,
            branchRegion: "Makkah",
            branchCity: "Jeddah");
        var service = CreateDispatchService(dbContext);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        decision.Should().BeNull();
        var assignments = await dbContext.DeliveryAssignments.ToListAsync();
        assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task TryAutoDispatchAsync_ShouldPenalizeLowConfidenceGpsInsideSameZone()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext, lowConfidenceFreshDriver: true);
        var service = CreateDispatchService(dbContext);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SecondSameZoneDriver.Id);
        decision.MatchReason.Should().Be("region-city-live-gps");
    }

    [Fact]
    public async Task AcceptOfferAsync_ShouldGeneratePickupOtpForAssignedDriver()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        var service = CreateDispatchService(dbContext);

        await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();

        await service.AcceptOfferAsync(assignment.Id, scenario.SameZoneFreshDriver.Id, CancellationToken.None);

        assignment.Status.Should().Be(AssignmentStatus.Accepted);
        assignment.PickupOtpCode.Should().NotBeNullOrWhiteSpace();
        assignment.PickupOtpExpiresAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptOfferAsync_WhenPickupDistanceDeviationIsHigh_ShouldCreatePricingReviewAlert()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        SetPrivateProperty(scenario.Order, nameof(Order.DriverToVendorDistanceKm), 0.01m);
        await dbContext.SaveChangesAsync();

        var adminAlertServiceMock = new Mock<IAdminAlertService>();
        adminAlertServiceMock
            .Setup(service => service.SendAsync(It.IsAny<AdminAlertRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAlertDispatchResult(0, 0, new OneSignalPushDispatchResult(false, false, true, null, null, null)));
        var service = CreateDispatchService(dbContext, adminAlertService: adminAlertServiceMock.Object);

        await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);
        var assignment = await dbContext.DeliveryAssignments.SingleAsync();

        await service.AcceptOfferAsync(assignment.Id, scenario.SameZoneFreshDriver.Id, CancellationToken.None);

        adminAlertServiceMock.Verify(
            service => service.SendAsync(
                It.Is<AdminAlertRequest>(request =>
                    request.Type == AdminAlertTypes.DeliveryPricingReviewRequired &&
                    request.Category == AdminAlertCategories.Delivery &&
                    request.ReferenceId == scenario.Order.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_WhenEnteringDispatchQueue_ShouldSendRealtimeCustomerStatusUpdate()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        var publisherMock = new Mock<IPublisher>();
        var service = CreateDispatchService(dbContext, publisher: publisherMock.Object);

        await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        publisherMock.Verify(
            publisher => publisher.Publish(
                It.Is<OrderStatusChangedNotification>(notification =>
                    notification.UserId == scenario.Order.UserId &&
                    notification.OrderId == scenario.Order.Id &&
                    notification.OrderNumber == scenario.Order.OrderNumber &&
                    notification.VendorId == scenario.Order.VendorId &&
                    notification.OldStatus == OrderStatus.ReadyForPickup &&
                    notification.NewStatus == OrderStatus.DriverAssignmentInProgress &&
                    notification.ActorRole == "dispatch" &&
                    notification.NotifyCustomer &&
                    !notification.NotifyVendor &&
                    !notification.CustomerNotificationAlreadySent),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_ShouldSendDeliveryOfferNotificationPayloadForDriverApp()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        var notificationServiceMock = new Mock<INotificationService>();
        var oneSignalPushServiceMock = new Mock<IOneSignalPushService>();
        oneSignalPushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "test",
                Reason: null));
        var service = CreateDispatchService(
            dbContext,
            notificationService: notificationServiceMock.Object,
            oneSignalPushService: oneSignalPushServiceMock.Object);

        await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();
        var expectedPayloadPart = $"\"assignmentId\":\"{assignment.Id}\"";

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                scenario.SameZoneFreshDriver.UserId,
                It.Is<NotificationDispatchRequest>(request =>
                    request.Type == NotificationTypes.DriverDeliveryOffer &&
                    request.Category == NotificationCategories.Dispatch &&
                    request.Priority == NotificationPriorities.Critical &&
                    request.ReferenceId == scenario.Order.Id &&
                    request.Data != null &&
                    request.Data.Contains("\"target\":\"driver-offer\"") &&
                    request.Data.Contains("\"presentation\":\"popup\"") &&
                    request.Data.Contains("\"popupType\":\"delivery_offer\"") &&
                    request.Data.Contains("\"showPopup\":true") &&
                    request.Data.Contains("\"eventName\":\"dispatch.offer_new\"") &&
                    request.Data.Contains("\"currentOffer\"") &&
                    (request.Data.Contains("\"pickupAddress\"") || request.Data.Contains("\"PickupAddress\"")) &&
                    (request.Data.Contains("\"orderItems\"") || request.Data.Contains("\"OrderItems\"")) &&
                    request.Data.Contains("\"vendorNameEn\":\"Dispatch Store\"") &&
                    request.Data.Contains($"\"driverId\":\"{scenario.SameZoneFreshDriver.Id}\"") &&
                    request.Data.Contains(expectedPayloadPart) &&
                    request.Data.Contains(scenario.Order.Id.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);

        oneSignalPushServiceMock.Verify(
            service => service.SendMobileNotificationDirectAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == scenario.SameZoneFreshDriver.UserId.ToString() &&
                    request.Type == NotificationTypes.DriverDeliveryOffer &&
                    request.ReferenceId == scenario.Order.Id &&
                    request.Category == NotificationCategories.Dispatch &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    request.TargetUrl == "/" &&
                    request.Data != null &&
                    request.Data.Contains("\"target\":\"driver-offer\"") &&
                    request.Data.Contains("\"presentation\":\"popup\"") &&
                    request.Data.Contains("\"popupType\":\"delivery_offer\"") &&
                    request.Data.Contains("\"showPopup\":true") &&
                    request.Data.Contains("\"eventName\":\"dispatch.offer_new\"") &&
                    request.Data.Contains("\"countdownSeconds\"") &&
                    request.Data.Contains("\"vendorNameEn\":\"Dispatch Store\"") &&
                    (request.Data.Contains("\"pickupAddress\"") || request.Data.Contains("\"PickupAddress\"")) &&
                    (request.Data.Contains("\"deliveryAddress\"") || request.Data.Contains("\"DeliveryAddress\"")) &&
                    request.Data.Contains("\"payout\"") &&
                    request.Data.Contains("\"itemsCount\"") &&
                    request.Data.Contains($"\"driverId\":\"{scenario.SameZoneFreshDriver.Id}\"") &&
                    request.Data.Contains(expectedPayloadPart) &&
                    request.Data.Contains(scenario.Order.Id.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_WhenOnlyDriverInSameRegionDifferentCity_ShouldOfferDriver()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(
            dbContext,
            branchRegion: "Eastern Province",
            branchCity: "Khobar",
            sameZoneDriverRegion: "EASTERN",
            sameZoneDriverCity: "DAMMAM",
            sameCityFallbackDriverRegion: "MAKKAH",
            sameCityFallbackDriverCity: "JEDDAH",
            secondSameZoneDriverRegion: "MAKKAH",
            secondSameZoneDriverCity: "JEDDAH");
        scenario.SameCityFallbackDriver.ToggleAvailability(false);
        scenario.SecondSameZoneDriver.ToggleAvailability(false);
        await dbContext.SaveChangesAsync();

        var service = CreateDispatchService(dbContext);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();
        assignment.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
        assignment.Status.Should().Be(AssignmentStatus.OfferSent);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_WhenOnlyAvailableDriverTimedOut_ShouldRetrySameDriver()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);
        scenario.SameCityFallbackDriver.ToggleAvailability(false);
        scenario.SecondSameZoneDriver.ToggleAvailability(false);
        await dbContext.SaveChangesAsync();

        var service = CreateDispatchService(dbContext);

        await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        var assignment = await dbContext.DeliveryAssignments.SingleAsync();
        var firstAttempt = await dbContext.DeliveryOfferAttempts.SingleAsync();
        assignment.MarkOfferTimedOut();
        firstAttempt.MarkTimedOut();
        await dbContext.SaveChangesAsync();

        var retryDecision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        retryDecision.Should().NotBeNull();
        retryDecision!.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);

        var assignments = await dbContext.DeliveryAssignments.ToListAsync();
        assignments.Should().ContainSingle();
        assignments[0].DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
        assignments[0].Status.Should().Be(AssignmentStatus.OfferSent);

        var attempts = await dbContext.DeliveryOfferAttempts
            .OrderBy(item => item.AttemptNumber)
            .ToListAsync();

        attempts.Should().HaveCount(2);
        attempts[0].Status.Should().Be(DeliveryOfferAttemptStatus.TimedOut);
        attempts[1].Status.Should().Be(DeliveryOfferAttemptStatus.Offered);
        attempts[1].DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
    }

    [Fact]
    public async Task TryAutoDispatchAsync_ShouldExcludeSoftBlockedDriverFromCandidates()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedDispatchScenarioAsync(dbContext);

        for (var index = 0; index < 20; index++)
        {
            var historicalAttempt = new DeliveryOfferAttempt(Guid.NewGuid(), null, scenario.SameZoneFreshDriver.Id, index + 1, DateTime.UtcNow.AddMinutes(1));
            historicalAttempt.MarkRejected("skip");
            dbContext.DeliveryOfferAttempts.Add(historicalAttempt);
        }

        await dbContext.SaveChangesAsync();

        var service = CreateDispatchService(dbContext);

        var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.DriverId.Should().Be(scenario.SecondSameZoneDriver.Id);
        var blockedDriver = await dbContext.Drivers.FindAsync(scenario.SameZoneFreshDriver.Id);
        blockedDriver!.IsAvailable.Should().BeFalse();
    }

    private static async Task<DispatchScenario> SeedDispatchScenarioAsync(
        ApplicationDbContext dbContext,
        bool lowConfidenceFreshDriver = false,
        string vendorRegion = "Riyadh",
        string vendorCity = "Riyadh",
        string branchRegion = "Riyadh",
        string branchCity = "Riyadh",
        string sameZoneDriverRegion = "RIYADH",
        string sameZoneDriverCity = "RIYADH",
        string sameCityFallbackDriverRegion = "RIYADH",
        string sameCityFallbackDriverCity = "RIYADH",
        string secondSameZoneDriverRegion = "RIYADH",
        string secondSameZoneDriverCity = "RIYADH")
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
            region: vendorRegion,
            city: vendorCity,
            nationalAddress: "Olaya",
            logoUrl: "https://example.com/dispatch-vendor-logo.png");

        var branch = new VendorBranch(
            vendor.Id,
            "Olaya Branch",
            "OLAYA",
            true,
            "King Fahd Rd",
            branchRegion,
            branchCity,
            24.7137m,
            46.6754m,
            "01000000998",
            "Dispatch Manager",
            "01000000999",
            8m);

        var sameZoneFreshDriver = new Driver(sameZoneUser.Id, DriverVehicleType.Car, "1234567890", "DRV-ZONE-1",
            region: sameZoneDriverRegion, city: sameZoneDriverCity);
        sameZoneFreshDriver.Approve(Guid.NewGuid());
        sameZoneFreshDriver.ToggleAvailability(true);

        var sameCityFallbackDriver = new Driver(secondZoneUser.Id, DriverVehicleType.Car, "1234567891", "DRV-CITY-1",
            region: sameCityFallbackDriverRegion, city: sameCityFallbackDriverCity);
        sameCityFallbackDriver.Approve(Guid.NewGuid());
        sameCityFallbackDriver.ToggleAvailability(true);

        var secondSameZoneDriver = new Driver(reserveZoneUser.Id, DriverVehicleType.Car, "1234567892", "DRV-ZONE-2",
            region: secondSameZoneDriverRegion, city: secondSameZoneDriverCity);
        secondSameZoneDriver.Approve(Guid.NewGuid());
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
            15m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
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

    private static void SetPrivateProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        property.Should().NotBeNull();
        property!.SetValue(target, value);
    }

    private sealed record DispatchScenario(
        Order Order,
        Driver SameZoneFreshDriver,
        Driver SameCityFallbackDriver,
        Driver SecondSameZoneDriver);
}
