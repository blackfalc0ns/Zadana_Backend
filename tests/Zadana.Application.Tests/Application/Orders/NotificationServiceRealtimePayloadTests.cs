using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Api.Realtime;
using Zadana.Api.Realtime.Contracts;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class NotificationServiceRealtimePayloadTests
{
    [Fact]
    public async Task SendOrderStatusChangedToUserAsync_ShouldSendCamelCasePayloadWithMobileStatus()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var (service, sent) = CreateNotificationService(userId);

        await service.SendOrderStatusChangedToUserAsync(
            userId,
            orderId,
            "ORD-REALTIME-001",
            vendorId,
            nameof(OrderStatus.PickedUp),
            nameof(OrderStatus.OnTheWay),
            "driver",
            "status_changed",
            $"/orders/{orderId}",
            CancellationToken.None);

        sent.Method.Should().Be(NotificationHub.ReceiveOrderStatusChangedMethod);
        var payload = sent.Payload.Should().BeOfType<OrderStatusChangedRealtimePayload>().Subject;
        payload.OrderId.Should().Be(orderId);
        payload.OrderNumber.Should().Be("ORD-REALTIME-001");
        payload.OldStatus.Should().Be("out_for_delivery");
        payload.NewStatus.Should().Be("out_for_delivery");
        payload.Presentation.Should().Be("popup");
        payload.PopupType.Should().Be("order_status_changed");
        payload.ShowPopup.Should().BeTrue();
        payload.OldStatusRaw.Should().Be(nameof(OrderStatus.PickedUp));
        payload.NewStatusRaw.Should().Be(nameof(OrderStatus.OnTheWay));

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"orderId\"");
        json.Should().Contain("\"orderNumber\"");
        json.Should().Contain("\"newStatus\":\"out_for_delivery\"");
        json.Should().Contain("\"newStatusRaw\":\"OnTheWay\"");
        json.Should().Contain("\"presentation\":\"popup\"");
        json.Should().Contain("\"popupType\":\"order_status_changed\"");
        json.Should().Contain("\"showPopup\":true");
        json.Should().Contain("\"changedAtUtc\"");
        json.Should().NotContain("\"OrderId\"");
    }

    [Fact]
    public async Task SendDriverArrivalStateChangedToUserAsync_ShouldSendExpectedCamelCasePayload()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var (service, sent) = CreateNotificationService(userId);

        await service.SendDriverArrivalStateChangedToUserAsync(
            userId,
            orderId,
            "ORD-REALTIME-002",
            "arrived_at_customer",
            "Driver User",
            "driver",
            $"/orders/{orderId}",
            CancellationToken.None);

        sent.Method.Should().Be(NotificationHub.ReceiveDriverArrivalStateChangedMethod);
        var payload = sent.Payload.Should().BeOfType<DriverArrivalStateChangedRealtimePayload>().Subject;
        payload.OrderId.Should().Be(orderId);
        payload.OrderNumber.Should().Be("ORD-REALTIME-002");
        payload.ArrivalState.Should().Be("arrived_at_customer");
        payload.DriverName.Should().Be("Driver User");
        payload.Presentation.Should().Be("popup");
        payload.PopupType.Should().Be("driver_arrival_state_changed");
        payload.ShowPopup.Should().BeTrue();

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"orderId\"");
        json.Should().Contain("\"orderNumber\"");
        json.Should().Contain("\"arrivalState\":\"arrived_at_customer\"");
        json.Should().Contain("\"driverName\":\"Driver User\"");
        json.Should().Contain("\"presentation\":\"popup\"");
        json.Should().Contain("\"popupType\":\"driver_arrival_state_changed\"");
        json.Should().Contain("\"showPopup\":true");
        json.Should().Contain("\"changedAtUtc\"");
        json.Should().NotContain("\"OrderId\"");
    }

    [Fact]
    public async Task SendOrderStatusChangedToUserAsync_WhenRefunded_ShouldUseRefundPopupType()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var (service, sent) = CreateNotificationService(userId);

        await service.SendOrderStatusChangedToUserAsync(
            userId,
            orderId,
            "ORD-REALTIME-REFUND-001",
            vendorId,
            nameof(OrderStatus.Delivered),
            nameof(OrderStatus.Refunded),
            "support",
            "status_changed",
            $"/orders/{orderId}",
            CancellationToken.None);

        sent.Method.Should().Be(NotificationHub.ReceiveOrderStatusChangedMethod);
        var payload = sent.Payload.Should().BeOfType<OrderStatusChangedRealtimePayload>().Subject;
        payload.PopupType.Should().Be("order_refund_status_changed");
        payload.Presentation.Should().Be("popup");
        payload.ShowPopup.Should().BeTrue();

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"popupType\":\"order_refund_status_changed\"");
        json.Should().Contain("\"showPopup\":true");
    }

    [Fact]
    public async Task SendOrderSupportCaseChangedToUserAsync_WhenReturnRequest_ShouldUseReturnPopupType()
    {
        var userId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var (service, sent) = CreateNotificationService(userId);

        await service.SendOrderSupportCaseChangedToUserAsync(
            userId,
            caseId,
            orderId,
            "ORD-RETURN-RT-001",
            "return_request",
            "in_review",
            "assigned",
            $"/orders/{orderId}/cases/{caseId}",
            CancellationToken.None);

        sent.Method.Should().Be(NotificationHub.ReceiveOrderSupportCaseChangedMethod);
        var payload = sent.Payload.Should().BeOfType<OrderSupportCaseChangedRealtimePayload>().Subject;
        payload.CaseId.Should().Be(caseId);
        payload.PopupType.Should().Be("return_request_status_update");
        payload.Presentation.Should().Be("popup");
        payload.ShowPopup.Should().BeTrue();

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"caseId\"");
        json.Should().Contain("\"popupType\":\"return_request_status_update\"");
        json.Should().Contain("\"showPopup\":true");
    }

    [Fact]
    public async Task SendDriverSupportCaseChangedToUserAsync_ShouldMarkRealtimePayloadAsPopup()
    {
        var userId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var (service, sent) = CreateNotificationService(userId);

        await service.SendDriverSupportCaseChangedToUserAsync(
            userId,
            caseId,
            driverId,
            orderId,
            "ORD-SUPPORT-RT-001",
            "driver_dispute",
            "in_review",
            "assigned",
            $"/orders/{orderId}/cases/{caseId}",
            CancellationToken.None);

        sent.Method.Should().Be(NotificationHub.ReceiveDriverSupportCaseChangedMethod);
        var payload = sent.Payload.Should().BeOfType<DriverSupportCaseChangedRealtimePayload>().Subject;
        payload.CaseId.Should().Be(caseId);
        payload.DriverId.Should().Be(driverId);
        payload.OrderId.Should().Be(orderId);
        payload.Presentation.Should().Be("popup");
        payload.PopupType.Should().Be("support_case_status_update");
        payload.ShowPopup.Should().BeTrue();

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"caseId\"");
        json.Should().Contain("\"presentation\":\"popup\"");
        json.Should().Contain("\"popupType\":\"support_case_status_update\"");
        json.Should().Contain("\"showPopup\":true");
        json.Should().NotContain("\"CaseId\"");
    }

    [Fact]
    public async Task SendDeliveryOfferToDriverAsync_ShouldMarkRealtimePayloadAsPopup()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var (service, sent) = CreateNotificationService(userId);

        var currentOffer = new Zadana.Application.Modules.Delivery.DTOs.DriverIncomingOfferDto(
            assignmentId,
            orderId,
            "ORD-OFFER-RT-001",
            "Vendor",
            "متجر",
            "Vendor",
            "https://example.com/logo.png",
            "Pickup address",
            24.7136m,
            46.6753m,
            "Customer",
            "Delivery address",
            24.7236m,
            46.6853m,
            3.5m,
            "14-19 min",
            15m,
            "CashOnDelivery",
            120m,
            100m,
            "VE",
            "CU",
            null,
            45,
            [new Zadana.Application.Modules.Delivery.DTOs.DriverOfferItemDto("Product", 1, null)]);

        await service.SendDeliveryOfferToDriverAsync(
            userId,
            currentOffer,
            CancellationToken.None);

        sent.Method.Should().Be(NotificationHub.ReceiveDeliveryOfferMethod);
        var payload = sent.Payload.Should().BeOfType<DeliveryOfferRealtimePayload>().Subject;
        payload.CurrentOffer.AssignmentId.Should().Be(assignmentId);
        payload.CurrentOffer.OrderId.Should().Be(orderId);
        payload.Presentation.Should().Be("popup");
        payload.PopupType.Should().Be("delivery_offer");
        payload.ShowPopup.Should().BeTrue();
        payload.EventName.Should().Be("dispatch.offer_new");
        payload.CurrentOffer.VendorNameAr.Should().Be("متجر");
        payload.CurrentOffer.VendorNameEn.Should().Be("Vendor");
        payload.CurrentOffer.VendorLogoUrl.Should().Be("https://example.com/logo.png");
        payload.CurrentOffer.OrderItems.Should().ContainSingle();

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        json.Should().Contain("\"presentation\":\"popup\"");
        json.Should().Contain("\"currentOffer\"");
        json.Should().Contain("vendorNameAr");
        json.Should().Contain("vendorNameEn");
        json.Should().Contain("vendorLogoUrl");
        json.Should().Contain("orderItems");
        json.Should().Contain("\"popupType\":\"delivery_offer\"");
        json.Should().Contain("\"showPopup\":true");
        json.Should().Contain("\"eventName\":\"dispatch.offer_new\"");
    }

    private static (NotificationService Service, SentSignalRMessage Sent) CreateNotificationService(Guid userId)
    {
        var sent = new SentSignalRMessage();
        var clientProxyMock = new Mock<IClientProxy>();
        clientProxyMock
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                sent.Method = method;
                sent.Payload = args.Single();
            })
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock
            .Setup(clients => clients.Group(NotificationHub.GetUserGroup(userId)))
            .Returns(clientProxyMock.Object);

        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        hubContextMock
            .SetupGet(context => context.Clients)
            .Returns(clientsMock.Object);

        return (
            new NotificationService(
                hubContextMock.Object,
                Mock.Of<IServiceScopeFactory>(),
                NullLogger<NotificationService>.Instance),
            sent);
    }

    private sealed class SentSignalRMessage
    {
        public string? Method { get; set; }
        public object? Payload { get; set; }
    }
}
