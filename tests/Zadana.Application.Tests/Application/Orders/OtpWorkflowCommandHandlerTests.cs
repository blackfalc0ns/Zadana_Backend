using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Modules.Delivery.Commands.VerifyAssignmentOtp;
using Zadana.Application.Modules.Orders.Commands.ConfirmVendorPickupOtp;
using Zadana.Application.Modules.Orders.Events;
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

public class OtpWorkflowCommandHandlerTests
{
    [Fact]
    public async Task ConfirmVendorPickupOtp_ShouldMarkOrderAndAssignmentPickedUpAndPublishStatusChange()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "otp.customer.pickup@test.com", "01000000130", UserRole.Customer);
        var vendorUser = new User("Vendor User", "otp.vendor.pickup@test.com", "01000000131", UserRole.Vendor);
        var driverUser = new User("Driver User", "otp.driver.pickup@test.com", "01000000132", UserRole.Driver);
        var vendor = CreateVendor(vendorUser.Id);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567899", "LIC-1003");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendor.Id, OrderStatus.DriverAssigned, "ORD-OTP-PICKUP-001");
        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkArrivedAtVendor();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));

        dbContext.Users.AddRange(customer, vendorUser, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var handler = new ConfirmVendorPickupOtpCommandHandler(dbContext, dbContext, publisherMock.Object);

        var result = await handler.Handle(
            new ConfirmVendorPickupOtpCommand(order.Id, vendor.Id, assignment.PickupOtpCode!),
            CancellationToken.None);

        result.Status.Should().Be("picked_up");
        order.Status.Should().Be(OrderStatus.PickedUp);
        assignment.Status.Should().Be(AssignmentStatus.PickedUp);
        assignment.IsPickupOtpVerified.Should().BeTrue();

        publisherMock.Verify(
            publisher => publisher.Publish(
                It.Is<OrderStatusChangedNotification>(notification =>
                    notification.OrderId == order.Id &&
                    notification.OldStatus == OrderStatus.DriverAssigned &&
                    notification.NewStatus == OrderStatus.PickedUp &&
                    notification.NotifyCustomer &&
                    !notification.NotifyVendor &&
                    notification.ActorRole == "vendor"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyAssignmentOtp_WhenDeliveryOtpIsVerified_ShouldMarkDeliveredAndPublishStatusChange()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "otp.customer.delivery@test.com", "01000000133", UserRole.Customer);
        var driverUser = new User("Driver User", "otp.driver.delivery@test.com", "01000000134", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "2234567899", "LIC-1004");
        driver.Approve(Guid.NewGuid());
        var vendorId = Guid.NewGuid();

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.OnTheWay, "ORD-OTP-DELIVERY-001");
        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();
        assignment.MarkArrivedAtCustomer();
        assignment.EnsureDeliveryOtp(TimeSpan.FromHours(4));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var handler = new VerifyAssignmentOtpCommandHandler(
            dbContext,
            dbContext,
            new DriverRepository(dbContext),
            publisherMock.Object);

        var result = await handler.Handle(
            new VerifyAssignmentOtpCommand(assignment.Id, driverUser.Id, "delivery", assignment.DeliveryOtpCode!),
            CancellationToken.None);

        result.Status.Should().Be("delivered");
        order.Status.Should().Be(OrderStatus.Delivered);
        assignment.Status.Should().Be(AssignmentStatus.Delivered);
        assignment.IsDeliveryOtpVerified.Should().BeTrue();

        publisherMock.Verify(
            publisher => publisher.Publish(
                It.Is<OrderStatusChangedNotification>(notification =>
                    notification.OrderId == order.Id &&
                    notification.OldStatus == OrderStatus.OnTheWay &&
                    notification.NewStatus == OrderStatus.Delivered &&
                    notification.NotifyCustomer &&
                    notification.NotifyVendor &&
                    notification.ActorRole == "driver"),
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

    private static Vendor CreateVendor(Guid userId) =>
        new(
            userId,
            "متجر OTP",
            "OTP Vendor",
            "Groceries",
            $"CR-{Guid.NewGuid():N}".Substring(0, 12),
            $"otp-vendor-{Guid.NewGuid():N}@test.com",
            "01000000135",
            city: "Riyadh",
            nationalAddress: "Olaya");

    private static Order CreateOrder(Guid userId, Guid vendorId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "OTP Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
