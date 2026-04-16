using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Modules.Orders.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class CustomerOrderReadServiceTests
{
    [Fact]
    public async Task GetCustomerOrdersAsync_ShouldClassifyOrdersIntoBuckets()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);

        dbContext.Orders.Add(CreateOrder(user.Id, OrderStatus.PendingVendorAcceptance, "ORD-ACTIVE"));
        dbContext.Orders.Add(CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLETED"));
        dbContext.Orders.Add(CreateOrder(user.Id, OrderStatus.Refunded, "ORD-RETURN"));
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var active = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Active, 1, 20);
        var completed = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Completed, 1, 20);
        var returns = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Returns, 1, 20);

        active.Items.Should().ContainSingle();
        active.Items[0].Status.Should().Be("pending");
        completed.Items.Should().ContainSingle();
        completed.Items[0].Status.Should().Be("delivered");
        returns.Items.Should().ContainSingle();
        returns.Items[0].Status.Should().Be("returning");
    }

    [Fact]
    public async Task GetCustomerOrderDetailAsync_ShouldMapCanCancelFromOrderStatus()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);

        var cancellable = CreateOrder(user.Id, OrderStatus.ReadyForPickup, "ORD-CAN-CANCEL");
        var locked = CreateOrder(user.Id, OrderStatus.DriverAssigned, "ORD-LOCKED");

        dbContext.Orders.AddRange(cancellable, locked);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var cancellableDetail = await service.GetCustomerOrderDetailAsync(cancellable.Id, user.Id);
        var lockedDetail = await service.GetCustomerOrderDetailAsync(locked.Id, user.Id);

        cancellableDetail!.CanCancel.Should().BeTrue();
        lockedDetail!.CanCancel.Should().BeFalse();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateUser() =>
        new("Customer User", "customer.orders@test.com", "01000000000", UserRole.Customer);

    private static Order CreateOrder(Guid userId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.CashOnDelivery, 100m, 0m, 10m, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
