using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverUpdateOrderStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenDriverIsNotApproved_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer.pending@test.com", "01000000115", UserRole.Customer);
        var driverUser = new User("Pending Driver", "driver-status.pending@test.com", "01000000116", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567892", "LIC-789");

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.DriverAssigned, "ORD-DRV-PENDING");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            Mock.Of<IPublisher>(),
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var action = () => handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.PickedUp, "Picked up"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DRIVER_NOT_READY_FOR_DISPATCH");
    }

    [Fact]
    public async Task Handle_WhenPickedUpWithoutPickupOtpVerification_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer@test.com", "01000000111", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver@test.com", "01000000112", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567890", "LIC-123");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.DriverAssigned, "ORD-DRV-PICKUP");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            Mock.Of<IPublisher>(),
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var action = () => handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.PickedUp, "Picked up"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "PICKUP_OTP_REQUIRED");
    }

    [Fact]
    public async Task Handle_WhenDeliveredWithoutVerifiedDeliveryOtp_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer.delivered@test.com", "01000000117", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver.delivered@test.com", "01000000118", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567895", "LIC-999");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.OnTheWay, "ORD-DRV-DELIVERED");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();
        assignment.EnsureDeliveryOtp(TimeSpan.FromHours(4));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            Mock.Of<IPublisher>(),
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var action = () => handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.Delivered, "Delivered"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DELIVERY_OTP_REQUIRED");
    }

    [Fact]
    public async Task Handle_WhenDeliveryFailedWithoutNote_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer2@test.com", "01000000113", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver2@test.com", "01000000114", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567891", "LIC-456");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.OnTheWay, "ORD-DRV-FAILED");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkPickedUp();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            Mock.Of<IPublisher>(),
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var action = () => handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.DeliveryFailed, null),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DELIVERY_FAILURE_NOTE_REQUIRED");
    }

    [Fact]
    public async Task Handle_WhenDeliveryOtpVerified_ShouldMarkDelivered()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer.success@test.com", "01000000119", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver.success@test.com", "01000000120", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567896", "LIC-1000");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.OnTheWay, "ORD-DRV-SUCCESS");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();
        assignment.EnsureDeliveryOtp(TimeSpan.FromHours(4));
        assignment.VerifyDeliveryOtp(driver.Id, assignment.DeliveryOtpCode!);

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            Mock.Of<IPublisher>(),
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var result = await handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.Delivered, "Delivered"),
            CancellationToken.None);

        result.Status.Should().Be("Delivered");
        assignment.Status.Should().Be(AssignmentStatus.Delivered);
        assignment.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenPickedUpAlreadyCompleted_ShouldReturnSuccessForLegacyEndpoint()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer.idempotent.pickup@test.com", "01000000121", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver.idempotent.pickup@test.com", "01000000122", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567897", "LIC-1001");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.PickedUp, "ORD-DRV-IDEMPOTENT-PICKUP");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            publisherMock.Object,
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var result = await handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.PickedUp, "Picked up"),
            CancellationToken.None);

        result.Status.Should().Be("PickedUp");
        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeliveredAlreadyCompleted_ShouldReturnSuccessForLegacyEndpoint()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer.idempotent.delivered@test.com", "01000000123", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver.idempotent.delivered@test.com", "01000000124", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567898", "LIC-1002");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.Delivered, "ORD-DRV-IDEMPOTENT-DELIVERED");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();
        assignment.EnsureDeliveryOtp(TimeSpan.FromHours(4));
        assignment.VerifyDeliveryOtp(driver.Id, assignment.DeliveryOtpCode!);
        assignment.MarkDelivered();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var handler = new DriverUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            publisherMock.Object,
            new DriverRepository(dbContext),
            Mock.Of<IDriverReadService>(),
            Mock.Of<INotificationService>());

        var result = await handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.Delivered, "Delivered"),
            CancellationToken.None);

        result.Status.Should().Be("Delivered");
        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static Order CreateOrder(Guid userId, Guid vendorId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Status Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
