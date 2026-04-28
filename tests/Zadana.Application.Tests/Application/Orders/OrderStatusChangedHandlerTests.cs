using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
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
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class OrderStatusChangedHandlerTests
{
    [Fact]
    public async Task Handle_WhenVendorReceivesNewOrder_ShouldSendInboxAndWebPush()
    {
        await using var dbContext = CreateDbContext();
        var vendorUser = CreateVendorUser();
        var vendor = CreateVendor(vendorUser.Id, newOrdersNotificationsEnabled: true);

        dbContext.Users.Add(vendorUser);
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var dispatcherMock = CreateDispatcherMock();
        var handler = new OrderStatusChangedHandler(
            notificationServiceMock.Object,
            dbContext,
            pushServiceMock.Object,
            dispatcherMock.Object);

        await handler.Handle(
            new OrderStatusChangedNotification(
                Guid.NewGuid(),
                Guid.NewGuid(),
                vendor.Id,
                "ORD-NEW-001",
                OrderStatus.PendingPayment,
                OrderStatus.PendingVendorAcceptance,
                NotifyCustomer: false,
                NotifyVendor: true,
                ActorRole: "payment_gateway"),
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                vendorUser.Id,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.VendorNewOrder,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                vendorUser.Id,
                It.IsAny<Guid>(),
                "ORD-NEW-001",
                vendor.Id,
                nameof(OrderStatus.PendingPayment),
                nameof(OrderStatus.PendingVendorAcceptance),
                "payment_gateway",
                "placed",
                It.Is<string>(url => url.Contains("/orders/")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                vendorUser.Id.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.VendorNewOrder,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(It.IsAny<OrderStatusCustomerNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenVendorReceivesCancellation_ShouldSendInboxAndWebPush()
    {
        await using var dbContext = CreateDbContext();
        var vendorUser = CreateVendorUser();
        var vendor = CreateVendor(vendorUser.Id, newOrdersNotificationsEnabled: false);

        dbContext.Users.Add(vendorUser);
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var dispatcherMock = CreateDispatcherMock();
        var handler = new OrderStatusChangedHandler(
            notificationServiceMock.Object,
            dbContext,
            pushServiceMock.Object,
            dispatcherMock.Object);

        await handler.Handle(
            new OrderStatusChangedNotification(
                Guid.NewGuid(),
                Guid.NewGuid(),
                vendor.Id,
                "ORD-CANCEL-001",
                OrderStatus.Accepted,
                OrderStatus.Cancelled,
                NotifyCustomer: false,
                NotifyVendor: true,
                ActorRole: "customer"),
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                vendorUser.Id,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.OrderCancelled,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                vendorUser.Id,
                It.IsAny<Guid>(),
                "ORD-CANCEL-001",
                vendor.Id,
                nameof(OrderStatus.Accepted),
                nameof(OrderStatus.Cancelled),
                "customer",
                "cancelled",
                It.Is<string>(url => url.Contains("/orders/")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                vendorUser.Id.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.OrderCancelled,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(It.IsAny<OrderStatusCustomerNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNewOrderPushIsDisabled_ShouldStillCreateInboxButSkipWebPush()
    {
        await using var dbContext = CreateDbContext();
        var vendorUser = CreateVendorUser();
        var vendor = CreateVendor(vendorUser.Id, newOrdersNotificationsEnabled: false);

        dbContext.Users.Add(vendorUser);
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var dispatcherMock = CreateDispatcherMock();
        var handler = new OrderStatusChangedHandler(
            notificationServiceMock.Object,
            dbContext,
            pushServiceMock.Object,
            dispatcherMock.Object);

        await handler.Handle(
            new OrderStatusChangedNotification(
                Guid.NewGuid(),
                Guid.NewGuid(),
                vendor.Id,
                "ORD-SILENT-001",
                OrderStatus.PendingPayment,
                OrderStatus.PendingVendorAcceptance,
                NotifyCustomer: false,
                NotifyVendor: true,
                ActorRole: "payment_gateway"),
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                vendorUser.Id,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.VendorNewOrder,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                vendorUser.Id,
                It.IsAny<Guid>(),
                "ORD-SILENT-001",
                vendor.Id,
                nameof(OrderStatus.PendingPayment),
                nameof(OrderStatus.PendingVendorAcceptance),
                "payment_gateway",
                "placed",
                It.Is<string>(url => url.Contains("/orders/")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                vendorUser.Id.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(It.IsAny<OrderStatusCustomerNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerShouldBeNotified_ShouldUseSharedDispatcher()
    {
        await using var dbContext = CreateDbContext();
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var dispatcherMock = CreateDispatcherMock();
        var handler = new OrderStatusChangedHandler(
            notificationServiceMock.Object,
            dbContext,
            pushServiceMock.Object,
            dispatcherMock.Object);
        var customerId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await handler.Handle(
            new OrderStatusChangedNotification(
                orderId,
                customerId,
                vendorId,
                "ORD-CUSTOMER-001",
                OrderStatus.PendingVendorAcceptance,
                OrderStatus.Accepted,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "vendor"),
            CancellationToken.None);

        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(
                It.Is<OrderStatusCustomerNotificationRequest>(request =>
                    request.UserId == customerId &&
                    request.OrderId == orderId &&
                    request.VendorId == vendorId &&
                    request.OrderNumber == "ORD-CUSTOMER-001" &&
                    request.OldStatus == OrderStatus.PendingVendorAcceptance &&
                    request.NewStatus == OrderStatus.Accepted &&
                    request.ActorRole == "vendor"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                customerId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        pushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                customerId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<OneSignalPushProfile>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerNotificationAlreadySent_ShouldSkipSharedDispatcher()
    {
        await using var dbContext = CreateDbContext();
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var dispatcherMock = CreateDispatcherMock();
        var handler = new OrderStatusChangedHandler(
            notificationServiceMock.Object,
            dbContext,
            pushServiceMock.Object,
            dispatcherMock.Object);

        await handler.Handle(
            new OrderStatusChangedNotification(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "ORD-CUSTOMER-DEDUP-001",
                OrderStatus.PendingVendorAcceptance,
                OrderStatus.Accepted,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "vendor",
                CustomerNotificationAlreadySent: true),
            CancellationToken.None);

        dispatcherMock.Verify(
            service => service.DispatchCustomerAsync(It.IsAny<OrderStatusCustomerNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAssignedDriverExists_ShouldSendRealtimeRefreshSignalToDriver()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.driver.signal@test.com", "01000000140", UserRole.Customer);
        var driverUser = new User("Driver User", "driver.signal@test.com", "01000000141", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "3234567890", "LIC-1005");
        var vendorId = Guid.NewGuid();
        var order = CreateOrder(customer.Id, vendorId, OrderStatus.DriverAssigned, "ORD-DRIVER-SIGNAL-001");
        var assignment = new DeliveryAssignment(order.Id, 0m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var dispatcherMock = CreateDispatcherMock();
        var handler = new OrderStatusChangedHandler(
            notificationServiceMock.Object,
            dbContext,
            pushServiceMock.Object,
            dispatcherMock.Object);

        await handler.Handle(
            new OrderStatusChangedNotification(
                order.Id,
                customer.Id,
                vendorId,
                order.OrderNumber,
                OrderStatus.DriverAssigned,
                OrderStatus.PickedUp,
                NotifyCustomer: false,
                NotifyVendor: false,
                ActorRole: "vendor"),
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                driverUser.Id,
                order.Id,
                order.OrderNumber,
                vendorId,
                nameof(OrderStatus.DriverAssigned),
                nameof(OrderStatus.PickedUp),
                "vendor",
                "status_changed",
                It.Is<string>(url => url.Contains("/orders/")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IOrderStatusNotificationDispatcher> CreateDispatcherMock()
    {
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
        return dispatcherMock;
    }

    private static Mock<IOneSignalPushService> CreatePushServiceMock()
    {
        var pushServiceMock = new Mock<IOneSignalPushService>();
        pushServiceMock
            .Setup(service => service.SendToExternalUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "push-id",
                Reason: null));
        pushServiceMock
            .Setup(service => service.SendToExternalUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<OneSignalPushProfile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "push-id",
                Reason: null));
        return pushServiceMock;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateVendorUser() =>
        new("Vendor Owner", "vendor.owner@test.com", "01000000020", UserRole.Vendor);

    private static Vendor CreateVendor(Guid userId, bool newOrdersNotificationsEnabled)
    {
        var vendor = new Vendor(
            userId,
            "متجر الاختبار",
            "Test Store",
            "Retail",
            "1234567890",
            "vendor@test.com",
            "01000000021");
        vendor.UpdateNotificationSettings(
            emailNotificationsEnabled: true,
            smsNotificationsEnabled: false,
            newOrdersNotificationsEnabled: newOrdersNotificationsEnabled);

        return vendor;
    }

    private static Order CreateOrder(Guid userId, Guid vendorId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Signal Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
