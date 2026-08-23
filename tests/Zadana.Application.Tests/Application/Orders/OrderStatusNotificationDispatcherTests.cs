using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class OrderStatusNotificationDispatcherTests
{
    [Fact]
    public void BuildDedupeKey_ForDelivered_ShouldCollapseRetriesToSingleKey()
    {
        var orderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var keyA = OrderStatusCustomerNotificationDedupe.BuildDedupeKey(orderId, OrderStatus.Delivered);
        var keyB = OrderStatusCustomerNotificationDedupe.BuildDedupeKey(orderId, OrderStatus.Delivered);

        keyA.Should().Be("order-status:22222222222222222222222222222222:Delivered");
        keyA.Should().Be(keyB);
        OrderStatusCustomerNotificationDedupe.CreateStableNotificationId(keyA)
            .Should().Be(OrderStatusCustomerNotificationDedupe.CreateStableNotificationId(keyB));
    }

    [Fact]
    public async Task DispatchCustomerAsync_ShouldPersistInboxAndUseProductionOrderStatusPushContract()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        notificationServiceMock
            .Setup(service => service.PersistToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        pushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == userId.ToString() &&
                    request.Type == NotificationTypes.OrderStatusChanged &&
                    request.ReferenceId == orderId &&
                    request.TargetUrl == $"/orders/{orderId}" &&
                    request.Category == NotificationCategories.Order &&
                    request.TargetApplication == OneSignalApplicationTarget.Customer &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "push-id",
                Reason: null));

        await using var dbContext = CreateDbContext();
        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            Mock.Of<ICustomerPresenceService>(),
            dbContext,
            NullLogger<OrderStatusNotificationDispatcher>.Instance);

        var result = await dispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                userId,
                orderId,
                vendorId,
                "ORD-DISPATCH-001",
                OrderStatus.PendingVendorAcceptance,
                OrderStatus.Accepted,
                ActorRole: "vendor"),
            CancellationToken.None);

        result.InboxQueued.Should().BeTrue();
        result.RealtimeQueued.Should().BeFalse();
        result.PushAttempted.Should().BeTrue();
        result.PushSent.Should().BeTrue();
        result.PushProviderStatusCode.Should().Be(200);

        notificationServiceMock.Verify(
            service => service.PersistToUserAsync(
                userId,
                "تم القبول",
                "Accepted",
                "طلب رقم ORD-DISPATCH-001",
                "Order #ORD-DISPATCH-001",
                NotificationTypes.OrderStatusChanged,
                orderId,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains("\"newStatus\":\"Accepted\"") &&
                    data.Contains("\"oldStatus\":\"PendingVendorAcceptance\"") &&
                    data.Contains($"\"dedupeKey\":\"order-status:{orderId:N}:Accepted\"") &&
                    data.Contains("\"action\":\"status_changed\"") &&
                    data.Contains("\"presentation\":\"silent\"") &&
                    data.Contains("\"popupType\":\"order_status_changed\"") &&
                    data.Contains("\"showPopup\":false")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()),
            Times.Never);

        pushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == userId.ToString() &&
                    request.TitleAr == "تم القبول" &&
                    request.TitleEn == "Accepted" &&
                    request.BodyAr == "طلب رقم ORD-DISPATCH-001" &&
                    request.BodyEn == "Order #ORD-DISPATCH-001" &&
                    request.Type == NotificationTypes.OrderStatusChanged &&
                    request.ReferenceId == orderId &&
                    request.Data != null &&
                    request.Data.Contains($"\"orderId\":\"{orderId}\"") &&
                    request.Data.Contains($"\"vendorId\":\"{vendorId}\"") &&
                    request.Data.Contains("\"orderNumber\":\"ORD-DISPATCH-001\"") &&
                    request.Data.Contains("\"oldStatus\":\"PendingVendorAcceptance\"") &&
                    request.Data.Contains("\"newStatus\":\"Accepted\"") &&
                    request.Data.Contains("\"actorRole\":\"vendor\"") &&
                    request.Data.Contains("\"action\":\"status_changed\"") &&
                    request.Data.Contains("\"presentation\":\"silent\"") &&
                    request.Data.Contains("\"popupType\":\"order_status_changed\"") &&
                    request.Data.Contains("\"showPopup\":false") &&
                    request.Data.Contains("\"targetUrl\":\"/orders/") &&
                    request.TargetUrl == $"/orders/{orderId}" &&
                    request.Category == NotificationCategories.Order &&
                    request.TargetApplication == OneSignalApplicationTarget.Customer &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchCustomerAsync_WhenStatusOutsideWhitelist_ShouldSkipCustomerNotification()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();

        await using var dbContext = CreateDbContext();
        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            Mock.Of<ICustomerPresenceService>(),
            dbContext,
            NullLogger<OrderStatusNotificationDispatcher>.Instance);

        var result = await dispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "ORD-SKIP-NOISE-001",
                OrderStatus.ReadyForPickup,
                OrderStatus.DriverAssignmentInProgress,
                ActorRole: "system"),
            CancellationToken.None);

        result.InboxQueued.Should().BeFalse();
        result.RealtimeQueued.Should().BeFalse();
        result.PushAttempted.Should().BeFalse();
        result.PushSent.Should().BeFalse();
        result.PushReason.Should().Contain("DriverAssignmentInProgress");

        notificationServiceMock.Verify(
            service => service.PersistToUserAsync(
                It.IsAny<Guid>(),
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
            service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchCustomerAsync_WhenCustomerIsForeground_ShouldSuppressDuplicatePush()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var presenceServiceMock = new Mock<ICustomerPresenceService>();
        var userId = Guid.NewGuid();

        notificationServiceMock
            .Setup(service => service.PersistToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        presenceServiceMock.Setup(service => service.IsOnline(userId)).Returns(true);

        await using var dbContext = CreateDbContext();
        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            presenceServiceMock.Object,
            dbContext,
            NullLogger<OrderStatusNotificationDispatcher>.Instance);

        var result = await dispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                userId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "ORD-FOREGROUND-001",
                OrderStatus.PendingPayment,
                OrderStatus.PendingVendorAcceptance,
                ActorRole: "customer"),
            CancellationToken.None);

        result.InboxQueued.Should().BeTrue();
        result.RealtimeQueued.Should().BeFalse();
        result.PushAttempted.Should().BeFalse();
        result.PushSent.Should().BeFalse();
        pushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchCustomerAsync_WhenPickupReadyAndForeground_ShouldStillSendHeadsUpPush()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var presenceServiceMock = new Mock<ICustomerPresenceService>();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        notificationServiceMock
            .Setup(service => service.PersistToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        presenceServiceMock.Setup(service => service.IsOnline(userId)).Returns(true);
        pushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "push-id", null));

        await using var dbContext = CreateDbContext();
        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            presenceServiceMock.Object,
            dbContext,
            NullLogger<OrderStatusNotificationDispatcher>.Instance);

        var result = await dispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                userId,
                orderId,
                Guid.NewGuid(),
                "ORD-PICKUP-READY-001",
                OrderStatus.Preparing,
                OrderStatus.ReadyForPickup,
                ActorRole: "vendor",
                Fulfillment: FulfillmentType.Pickup),
            CancellationToken.None);

        result.RealtimeQueued.Should().BeFalse();
        result.PushAttempted.Should().BeTrue();
        result.PushSent.Should().BeTrue();
        pushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == userId.ToString() &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    request.Data != null &&
                    request.Data.Contains("\"showPopup\":false") &&
                    request.Data.Contains("\"eventName\":\"order.pickup.ready\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchCustomerAsync_WhenOrderRefunded_ShouldUseRefundPopupContract()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        notificationServiceMock
            .Setup(service => service.PersistToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        pushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "push-id", null));

        await using var dbContext = CreateDbContext();
        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            Mock.Of<ICustomerPresenceService>(),
            dbContext,
            NullLogger<OrderStatusNotificationDispatcher>.Instance);

        await dispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                userId,
                orderId,
                vendorId,
                "ORD-REFUND-001",
                OrderStatus.Delivered,
                OrderStatus.Refunded,
                ActorRole: "support"),
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()),
            Times.Never);

        pushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == userId.ToString() &&
                    request.Type == NotificationTypes.OrderStatusChanged &&
                    request.ReferenceId == orderId &&
                    request.TargetUrl == $"/orders/{orderId}" &&
                    request.Category == NotificationCategories.Order &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    request.TargetApplication == OneSignalApplicationTarget.Customer &&
                    request.Data != null &&
                    request.Data.Contains("\"presentation\":\"silent\"") &&
                    request.Data.Contains("\"popupType\":\"order_refund_status_changed\"") &&
                    request.Data.Contains("\"showPopup\":false") &&
                    request.Data.Contains("\"eventName\":\"order.refund.refunded\"") &&
                    request.Data.Contains("\"isRefund\":true") &&
                    request.Data.Contains("\"refundStatus\":\"refunded\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchCustomerAsync_WhenSameStatusAlreadyPersisted_ShouldSuppressDuplicatePush()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        await using var dbContext = CreateDbContext();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dedupeKey = OrderStatusCustomerNotificationDedupe.BuildDedupeKey(orderId, OrderStatus.Delivered);

        dbContext.Notifications.Add(new Notification(
            userId,
            "تم التسليم",
            "Delivered",
            "طلب رقم ORD-DUP-001",
            "Order #ORD-DUP-001",
            NotificationTypes.OrderStatusChanged,
            null,
            null,
            orderId,
            $$"""{"dedupeKey":"{{dedupeKey}}","orderId":"{{orderId}}"}"""));
        await dbContext.SaveChangesAsync();

        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            Mock.Of<ICustomerPresenceService>(),
            dbContext,
            NullLogger<OrderStatusNotificationDispatcher>.Instance);

        var result = await dispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                userId,
                orderId,
                Guid.NewGuid(),
                "ORD-DUP-001",
                OrderStatus.OnTheWay,
                OrderStatus.Delivered,
                ActorRole: "driver"),
            CancellationToken.None);

        result.InboxQueued.Should().BeFalse();
        result.PushAttempted.Should().BeFalse();
        result.PushReason.Should().Contain("Duplicate suppressed");

        notificationServiceMock.Verify(
            service => service.PersistToUserAsync(
                It.IsAny<Guid>(),
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
            service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
