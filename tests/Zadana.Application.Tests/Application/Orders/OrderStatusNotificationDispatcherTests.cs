using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class OrderStatusNotificationDispatcherTests
{
    [Fact]
    public async Task DispatchCustomerAsync_ShouldQueueInboxRealtimeAndUseProductionOrderStatusPushContract()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        notificationServiceMock
            .Setup(service => service.SendToUserAsync(
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
        notificationServiceMock
            .Setup(service => service.SendOrderStatusChangedToUserAsync(
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
                It.IsAny<string?>()))
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

        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            Mock.Of<ICustomerPresenceService>(),
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
        result.RealtimeQueued.Should().BeTrue();
        result.PushAttempted.Should().BeTrue();
        result.PushSent.Should().BeTrue();
        result.PushProviderStatusCode.Should().Be(200);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                userId,
                It.IsAny<string>(),
                "Order Accepted",
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("ORD-DISPATCH-001")),
                NotificationTypes.OrderStatusChanged,
                orderId,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains("\"newStatus\":\"Accepted\"") &&
                    data.Contains("\"oldStatus\":\"PendingVendorAcceptance\"") &&
                    data.Contains("\"dedupeKey\":\"order-status:") &&
                    data.Contains("\"action\":\"status_changed\"") &&
                    data.Contains("\"presentation\":\"popup\"") &&
                    data.Contains("\"popupType\":\"order_status_changed\"") &&
                    data.Contains("\"showPopup\":true")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                userId,
                orderId,
                "ORD-DISPATCH-001",
                vendorId,
                OrderStatus.PendingVendorAcceptance.ToString(),
                OrderStatus.Accepted.ToString(),
                "vendor",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == userId.ToString() &&
                    request.TitleEn == "Order Accepted" &&
                    request.BodyEn.Contains("ORD-DISPATCH-001") &&
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
                    request.Data.Contains("\"presentation\":\"popup\"") &&
                    request.Data.Contains("\"popupType\":\"order_status_changed\"") &&
                    request.Data.Contains("\"showPopup\":true") &&
                    request.Data.Contains("\"targetUrl\":\"/orders/") &&
                    request.TargetUrl == $"/orders/{orderId}" &&
                    request.Category == NotificationCategories.Order &&
                    request.TargetApplication == OneSignalApplicationTarget.Customer &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchCustomerAsync_WhenCustomerIsForeground_ShouldSuppressDuplicatePush()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var presenceServiceMock = new Mock<ICustomerPresenceService>();
        var userId = Guid.NewGuid();

        notificationServiceMock
            .Setup(service => service.SendToUserAsync(
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
        notificationServiceMock
            .Setup(service => service.SendOrderStatusChangedToUserAsync(
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
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        presenceServiceMock.Setup(service => service.IsOnline(userId)).Returns(true);

        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            presenceServiceMock.Object,
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
        result.RealtimeQueued.Should().BeTrue();
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
            .Setup(service => service.SendToUserAsync(
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
        notificationServiceMock
            .Setup(service => service.SendOrderStatusChangedToUserAsync(
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
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        presenceServiceMock.Setup(service => service.IsOnline(userId)).Returns(true);
        pushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "push-id", null));

        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            presenceServiceMock.Object,
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

        result.RealtimeQueued.Should().BeTrue();
        result.PushAttempted.Should().BeTrue();
        result.PushSent.Should().BeTrue();
        pushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == userId.ToString() &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    request.Data != null &&
                    request.Data.Contains("\"showPopup\":true") &&
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
            .Setup(service => service.SendToUserAsync(
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
        notificationServiceMock
            .Setup(service => service.SendOrderStatusChangedToUserAsync(
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
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        pushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "push-id", null));

        var dispatcher = new OrderStatusNotificationDispatcher(
            notificationServiceMock.Object,
            pushServiceMock.Object,
            Mock.Of<ICustomerPresenceService>(),
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
                    request.Data.Contains("\"presentation\":\"popup\"") &&
                    request.Data.Contains("\"popupType\":\"order_refund_status_changed\"") &&
                    request.Data.Contains("\"showPopup\":true") &&
                    request.Data.Contains("\"eventName\":\"order.refund.refunded\"") &&
                    request.Data.Contains("\"isRefund\":true") &&
                    request.Data.Contains("\"refundStatus\":\"refunded\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
