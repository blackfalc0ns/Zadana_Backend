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
            .Setup(service => service.SendToExternalUserAsync(
                userId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.OrderStatusChanged,
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
        result.RealtimeQueued.Should().BeFalse();
        result.PushAttempted.Should().BeTrue();
        result.PushSent.Should().BeTrue();
        result.PushProviderStatusCode.Should().Be(200);

        notificationServiceMock.Verify(
            service => service.PersistToUserAsync(
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
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

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
                    data.Contains($"\"orderId\":\"{orderId}\"") &&
                    data.Contains($"\"vendorId\":\"{vendorId}\"") &&
                    data.Contains("\"orderNumber\":\"ORD-DISPATCH-001\"") &&
                    data.Contains("\"oldStatus\":\"PendingVendorAcceptance\"") &&
                    data.Contains("\"newStatus\":\"Accepted\"") &&
                    data.Contains("\"actorRole\":\"vendor\"") &&
                    data.Contains("\"action\":\"status_changed\"") &&
                    data.Contains("\"targetUrl\":\"/orders/")),
                $"/orders/{orderId}",
                OneSignalPushProfile.MobileHeadsUp,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
