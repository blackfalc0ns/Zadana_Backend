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

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"orderId\"");
        json.Should().Contain("\"orderNumber\"");
        json.Should().Contain("\"newStatus\":\"out_for_delivery\"");
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

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"orderId\"");
        json.Should().Contain("\"orderNumber\"");
        json.Should().Contain("\"arrivalState\":\"arrived_at_customer\"");
        json.Should().Contain("\"driverName\":\"Driver User\"");
        json.Should().Contain("\"changedAtUtc\"");
        json.Should().NotContain("\"OrderId\"");
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
