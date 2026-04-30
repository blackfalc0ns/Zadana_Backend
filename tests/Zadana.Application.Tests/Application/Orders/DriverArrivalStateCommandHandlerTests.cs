using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverArrivalState;
using Zadana.Application.Modules.Delivery.Interfaces;
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

        var order = new Order("ORD-ARR-001", customer.Id, vendor.Id, Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 5m);
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
            notificationService.Object);

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

        var customer = new User("Customer User", "arrival.customer2@test.com", "01000000134", UserRole.Customer);
        var vendorUser = new User("Vendor User", "arrival.vendor2@test.com", "01000000135", UserRole.Vendor);
        var driverUser = new User("Driver User", "arrival.driver2@test.com", "01000000136", UserRole.Driver);
        var vendor = new Vendor(vendorUser.Id, "متجر", "Store", "Groceries", "CR-ARR-2", "arrival.vendor2@test.com", "01000000135", city: "Riyadh");
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567891", "CAR-124");
        driver.Approve(Guid.NewGuid());

        var order = new Order("ORD-ARR-002", customer.Id, vendor.Id, Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 5m);
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
            notificationService.Object);

        var result = await handler.Handle(
            new UpdateDriverArrivalStateCommand(order.Id, driverUser.Id, "arrived_at_customer"),
            CancellationToken.None);

        result.ArrivalState.Should().Be("arrived_at_customer");
        assignment.Status.Should().Be(AssignmentStatus.ArrivedAtCustomer);
        assignment.ArrivedAtCustomerAtUtc.Should().NotBeNull();
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
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
