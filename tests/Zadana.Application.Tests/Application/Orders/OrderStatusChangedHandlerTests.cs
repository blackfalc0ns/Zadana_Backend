using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
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
        var handler = new OrderStatusChangedHandler(notificationServiceMock.Object, dbContext, pushServiceMock.Object);

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
        var handler = new OrderStatusChangedHandler(notificationServiceMock.Object, dbContext, pushServiceMock.Object);

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
        var handler = new OrderStatusChangedHandler(notificationServiceMock.Object, dbContext, pushServiceMock.Object);

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
    }

    [Fact]
    public async Task Handle_WhenCustomerShouldBeNotified_ShouldSendInboxAndRealtimeOrderEvent()
    {
        await using var dbContext = CreateDbContext();
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = CreatePushServiceMock();
        var handler = new OrderStatusChangedHandler(notificationServiceMock.Object, dbContext, pushServiceMock.Object);
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

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                customerId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.OrderStatusChanged,
                orderId,
                It.Is<string?>(data => data != null && data.Contains("\"newStatus\":\"Accepted\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                customerId,
                orderId,
                "ORD-CUSTOMER-001",
                vendorId,
                nameof(OrderStatus.PendingVendorAcceptance),
                nameof(OrderStatus.Accepted),
                "vendor",
                "status_changed",
                $"/orders/{orderId}",
                It.IsAny<CancellationToken>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                customerId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.OrderStatusChanged,
                orderId,
                It.Is<string?>(data => data != null && data.Contains("\"newStatus\":\"Accepted\"")),
                $"/orders/{orderId}",
                OneSignalPushProfile.MobileHeadsUp,
                It.IsAny<CancellationToken>()),
            Times.Once);
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
}
