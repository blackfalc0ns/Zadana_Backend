using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Modules.Orders.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class VendorOrderReadServiceTests
{
    [Fact]
    public async Task GetVendorWorkspaceOrdersAsync_ShouldExcludePendingPaymentOrders()
    {
        await using var dbContext = CreateDbContext();
        var vendorId = Guid.NewGuid();
        var customer = CreateCustomer();

        dbContext.Users.Add(customer);
        dbContext.Orders.Add(CreateOrder(customer.Id, vendorId, OrderStatus.PendingPayment, "ORD-PAYMENT-PENDING"));
        dbContext.Orders.Add(CreateOrder(customer.Id, vendorId, OrderStatus.PendingVendorAcceptance, "ORD-VISIBLE"));
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetVendorWorkspaceOrdersAsync(vendorId, null, null, null, 1, 20);

        result.Items.Should().ContainSingle();
        result.Items[0].OrderNumber.Should().Be("ORD-VISIBLE");
    }

    [Fact]
    public async Task GetVendorOrderDetailAsync_ShouldReturnNullForPendingPaymentOrder()
    {
        await using var dbContext = CreateDbContext();
        var vendorId = Guid.NewGuid();
        var customer = CreateCustomer();
        var pendingPaymentOrder = CreateOrder(customer.Id, vendorId, OrderStatus.PendingPayment, "ORD-HIDDEN");

        dbContext.Users.Add(customer);
        dbContext.Orders.Add(pendingPaymentOrder);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetVendorOrderDetailAsync(vendorId, pendingPaymentOrder.Id);

        result.Should().BeNull();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateCustomer() =>
        new("Customer User", "vendor.orders.customer@test.com", "01000000011", UserRole.Customer);

    private static Order CreateOrder(Guid userId, Guid vendorId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
