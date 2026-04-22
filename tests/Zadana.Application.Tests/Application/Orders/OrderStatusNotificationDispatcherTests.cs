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
    public async Task DispatchCustomerAsync_ShouldQueueInboxRealtimeAndHeadsUpPush()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        pushServiceMock
            .Setup(service => service.SendToExternalUserAsync(
                userId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                orderId,
                It.IsAny<string?>(),
                $"/orders/{orderId}",
                OneSignalPushProfile.MobileHeadsUp,
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
                    data.Contains("\"action\":\"status_changed\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendOrderStatusChangedToUserAsync(
                userId,
                orderId,
                "ORD-DISPATCH-001",
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
                userId.ToString(),
                It.IsAny<string>(),
                "Order Accepted",
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("ORD-DISPATCH-001")),
                NotificationTypes.OrderStatusChanged,
                orderId,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains("\"newStatus\":\"Accepted\"") &&
                    data.Contains("\"targetUrl\":\"/orders/")),
                $"/orders/{orderId}",
                OneSignalPushProfile.MobileHeadsUp,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
