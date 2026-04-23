using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;
using Zadana.Domain.Modules.Delivery.Entities;
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
    public async Task Handle_WhenDeliveredWithoutPhotoProof_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer@test.com", "01000000111", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver@test.com", "01000000112", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, "Car", "1234567890", "LIC-123");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.OnTheWay, "ORD-DRV-DELIVERED");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id);
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
            new DriverRepository(dbContext));

        var action = () => handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.Delivered, "Delivered"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DELIVERY_PROOF_REQUIRED");
    }

    [Fact]
    public async Task Handle_WhenDeliveryFailedWithoutNote_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "driver-status.customer2@test.com", "01000000113", UserRole.Customer);
        var driverUser = new User("Driver User", "driver-status.driver2@test.com", "01000000114", UserRole.Driver);
        var vendorId = Guid.NewGuid();

        var driver = new Driver(driverUser.Id, "Car", "1234567891", "LIC-456");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.OnTheWay, "ORD-DRV-FAILED");
        var assignment = new DeliveryAssignment(order.Id, 0);
        assignment.OfferTo(driver.Id);
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
            new DriverRepository(dbContext));

        var action = () => handler.Handle(
            new DriverUpdateOrderStatusCommand(order.Id, driverUser.Id, OrderStatus.DeliveryFailed, null),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DELIVERY_FAILURE_NOTE_REQUIRED");
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
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Status Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
