using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverArrivalState;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverArrivalStateCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenArrivedAtVendor_ShouldNotifyVendorInRealtime()
    {
        await using var dbContext = CreateDbContext();
        var notificationService = new Mock<INotificationService>();

        var customer = new User("Customer User", "arrival.customer@test.com", "01000000131", UserRole.Customer);
        var vendorUser = new User("Vendor User", "arrival.vendor@test.com", "01000000132", UserRole.Vendor);
        var driverUser = new User("Driver User", "arrival.driver@test.com", "01000000133", UserRole.Driver);
        var vendor = new Vendor(vendorUser.Id, "متجر", "Store", "Groceries", "CR-ARR-1", "arrival.vendor@test.com", "01000000132", city: "Riyadh");
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567890", "CAR-123");
        driver.Approve(Guid.NewGuid());

        var order = new Order("ORD-ARR-001", customer.Id, vendor.Id, Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m, null, null, false, null, null, null, null, 1, false, 5m);
        order.ChangeStatus(OrderStatus.DriverAssigned);

        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        dbContext.Users.AddRange(customer, vendorUser, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverArrivalStateCommandHandler(
            dbContext,
            dbContext,
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            notificationService.Object,
            Mock.Of<IOneSignalPushService>(),
            Mock.Of<IOrderTrackingRealtimeNotifier>(),
            Mock.Of<IEmailCenterService>(),
            NullLogger<UpdateDriverArrivalStateCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateDriverArrivalStateCommand(order.Id, driverUser.Id, "arrived_at_vendor"),
            CancellationToken.None);

        result.ArrivalState.Should().Be("arrived_at_vendor");
        assignment.Status.Should().Be(AssignmentStatus.ArrivedAtVendor);
        assignment.ArrivedAtVendorAtUtc.Should().NotBeNull();
        notificationService.Verify(
            service => service.SendDriverArrivalStateChangedToUserAsync(
                vendorUser.Id,
                order.Id,
                order.OrderNumber,
                "arrived_at_vendor",
                driverUser.FullName,
                "driver",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenArrivedAtCustomer_ShouldNotifyCustomerInRealtime()
    {
        await using var dbContext = CreateDbContext();
        var notificationService = new Mock<INotificationService>();
        var pushService = new Mock<IOneSignalPushService>();
        var emailCenterService = new Mock<IEmailCenterService>();
        pushService
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "arrival-push-id",
                Reason: null));
        emailCenterService
            .Setup(service => service.DispatchSystemEventEmailAsync(
                It.IsAny<EmailSystemEventDispatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDispatchOperationResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                Source: "system_event",
                Provider: "test",
                ProviderMessageId: "arrival-email-id",
                Reason: null));

        var customer = new User("Customer User", "arrival.customer2@test.com", "01000000134", UserRole.Customer);
        var vendorUser = new User("Vendor User", "arrival.vendor2@test.com", "01000000135", UserRole.Vendor);
        var driverUser = new User("Driver User", "arrival.driver2@test.com", "01000000136", UserRole.Driver);
        var vendor = new Vendor(vendorUser.Id, "متجر", "Store", "Groceries", "CR-ARR-2", "arrival.vendor2@test.com", "01000000135", city: "Riyadh");
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567891", "CAR-124");
        driver.Approve(Guid.NewGuid());

        var order = new Order("ORD-ARR-002", customer.Id, vendor.Id, Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m, null, null, false, null, null, null, null, 1, false, 5m);
        order.ChangeStatus(OrderStatus.DriverAssigned);
        order.ChangeStatus(OrderStatus.PickedUp);
        order.ChangeStatus(OrderStatus.OnTheWay);

        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkPickedUp();

        dbContext.Users.AddRange(customer, vendorUser, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverArrivalStateCommandHandler(
            dbContext,
            dbContext,
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            notificationService.Object,
            pushService.Object,
            Mock.Of<IOrderTrackingRealtimeNotifier>(),
            emailCenterService.Object,
            NullLogger<UpdateDriverArrivalStateCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateDriverArrivalStateCommand(order.Id, driverUser.Id, "arrived_at_customer"),
            CancellationToken.None);

        result.ArrivalState.Should().Be("arrived_at_customer");
        assignment.Status.Should().Be(AssignmentStatus.ArrivedAtCustomer);
        assignment.ArrivedAtCustomerAtUtc.Should().NotBeNull();
        notificationService.Verify(
            service => service.SendToUserAsync(
                customer.Id,
                It.IsAny<string>(),
                "Driver arrived at delivery location",
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("ORD-ARR-002")),
                "driver-arrival",
                order.Id,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains("\"arrivalState\":\"arrived_at_customer\"") &&
                    data.Contains("\"screen\":\"order_tracking\"") &&
                    data.Contains("\"presentation\":\"popup\"") &&
                    data.Contains("\"popupType\":\"driver_arrival_state_changed\"") &&
                    data.Contains("\"showPopup\":true")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        notificationService.Verify(
            service => service.SendDriverArrivalStateChangedToUserAsync(
                customer.Id,
                order.Id,
                order.OrderNumber,
                "arrived_at_customer",
                driverUser.FullName,
                "driver",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        pushService.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == customer.Id.ToString() &&
                    request.TitleEn == "Driver arrived at delivery location" &&
                    request.BodyEn.Contains("ORD-ARR-002") &&
                    request.Type == "driver-arrival" &&
                    request.ReferenceId == order.Id &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    request.TargetApplication == OneSignalApplicationTarget.Customer &&
                    request.TargetUrl == $"/orders/{order.Id}" &&
                    request.Data != null &&
                    request.Data.Contains("\"arrivalState\":\"arrived_at_customer\"") &&
                    request.Data.Contains("\"presentation\":\"popup\"") &&
                    request.Data.Contains("\"popupType\":\"driver_arrival_state_changed\"") &&
                    request.Data.Contains("\"showPopup\":true")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        emailCenterService.Verify(
            service => service.DispatchSystemEventEmailAsync(
                It.Is<EmailSystemEventDispatchRequest>(request =>
                    request.EventKey == EmailEventKeys.CustomerDriverArrivedAtDelivery &&
                    request.AudienceType == "customers" &&
                    request.To.Single() == customer.Email &&
                    request.EntityId == order.Id &&
                    request.VendorId == vendor.Id &&
                    request.TargetUrl == $"/orders/{order.Id}" &&
                    request.Variables != null &&
                    request.Variables["customer_name"] == customer.FullName &&
                    request.Variables["order_number"] == order.OrderNumber &&
                    request.Variables["vendor_name"] == vendor.BusinessNameEn),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
