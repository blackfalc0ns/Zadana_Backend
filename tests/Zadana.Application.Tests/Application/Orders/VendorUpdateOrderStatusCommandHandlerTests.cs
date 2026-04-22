using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Commands.VendorUpdateOrderStatus;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class VendorUpdateOrderStatusCommandHandlerTests
{
    [Theory]
    [InlineData(OrderStatus.PendingVendorAcceptance, OrderStatus.Accepted)]
    [InlineData(OrderStatus.PendingVendorAcceptance, OrderStatus.VendorRejected)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.ReadyForPickup)]
    public async Task Handle_WhenVendorUpdatesStatus_ShouldDispatchCustomerDirectlyAndPublishDedupedEvent(
        OrderStatus currentStatus,
        OrderStatus newStatus)
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var vendorId = Guid.NewGuid();
        var order = CreateOrder(customer.Id, vendorId, currentStatus, $"ORD-{newStatus}");

        dbContext.Users.Add(customer);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var dispatcherMock = new Mock<IOrderStatusNotificationDispatcher>();
        dispatcherMock
            .Setup(service => service.DispatchCustomerAsync(
                It.IsAny<OrderStatusCustomerNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusNotificationDispatchResult(
                InboxQueued: true,
                RealtimeQueued: true,
                PushAttempted: true,
                PushSent: true,
                PushProviderStatusCode: 200,
                PushReason: null));

        var handler = new VendorUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            publisherMock.Object,
            dispatcherMock.Object);

        var result = await handler.Handle(
            new VendorUpdateOrderStatusCommand(order.Id, vendorId, newStatus, "vendor update note"),
            CancellationToken.None);

        result.OrderId.Should().Be(order.Id);
        result.Status.Should().Be(newStatus.ToString());
        order.Status.Should().Be(newStatus);

        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(
                It.Is<OrderStatusCustomerNotificationRequest>(request =>
                    request.UserId == customer.Id &&
                    request.OrderId == order.Id &&
                    request.VendorId == vendorId &&
                    request.OrderNumber == order.OrderNumber &&
                    request.OldStatus == currentStatus &&
                    request.NewStatus == newStatus &&
                    request.ActorRole == "vendor"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        publisherMock.Verify(
            publisher => publisher.Publish(
                It.Is<OrderStatusChangedNotification>(notification =>
                    notification.OrderId == order.Id &&
                    notification.UserId == customer.Id &&
                    notification.VendorId == vendorId &&
                    notification.OrderNumber == order.OrderNumber &&
                    notification.OldStatus == currentStatus &&
                    notification.NewStatus == newStatus &&
                    notification.NotifyCustomer &&
                    !notification.NotifyVendor &&
                    notification.ActorRole == "vendor" &&
                    notification.CustomerNotificationAlreadySent),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStatusAlreadyMatchesTarget_ShouldSkipDispatchAndPublish()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var vendorId = Guid.NewGuid();
        var order = CreateOrder(customer.Id, vendorId, OrderStatus.Accepted, "ORD-IDEMPOTENT-001");

        dbContext.Users.Add(customer);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var dispatcherMock = new Mock<IOrderStatusNotificationDispatcher>();
        var handler = new VendorUpdateOrderStatusCommandHandler(
            dbContext,
            dbContext,
            publisherMock.Object,
            dispatcherMock.Object);

        var result = await handler.Handle(
            new VendorUpdateOrderStatusCommand(order.Id, vendorId, OrderStatus.Accepted, null),
            CancellationToken.None);

        result.Status.Should().Be(nameof(OrderStatus.Accepted));
        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(It.IsAny<OrderStatusCustomerNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

    private static User CreateCustomer() =>
        new("Customer User", "customer.vendorstatus@test.com", "01000000111", UserRole.Customer);

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
