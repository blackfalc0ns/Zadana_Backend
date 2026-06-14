using System.Text;
using System.Text.Json;
using FluentAssertions;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverNotificationDataBuilderTests
{
    [Fact]
    public void Build_ForWalletEvent_ShouldIncludePopupContractAndTargetUrl()
    {
        var withdrawalId = Guid.NewGuid();

        var json = DriverNotificationDataBuilder.Build(
            screen: "wallet",
            @event: "wallet.withdrawal_submitted",
            withdrawalId: withdrawalId,
            extra: new
            {
                amount = 120m
            });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("presentation").GetString().Should().Be("popup");
        root.GetProperty("popupType").GetString().Should().Be("driver_wallet_updated");
        root.GetProperty("showPopup").GetBoolean().Should().BeTrue();
        root.GetProperty("eventName").GetString().Should().Be("wallet.withdrawal_submitted");
        root.GetProperty("targetUrl").GetString().Should().Be($"/wallet/withdrawals/{withdrawalId}");
        root.GetProperty("category").GetString().Should().Be("wallet");
    }

    [Fact]
    public void Build_ForAssignmentDetail_ShouldResolveAssignmentTargetUrl()
    {
        var assignmentId = Guid.NewGuid();

        var json = DriverNotificationDataBuilder.Build(
            screen: "assignment_detail",
            @event: "assignment.driver_assigned",
            assignmentId: assignmentId);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("targetUrl").GetString()
            .Should().Be($"/assignments/{assignmentId}");
    }

    [Fact]
    public void Build_ForDeliveryOffer_ShouldKeepDeliveryOfferPopupType()
    {
        var assignmentId = Guid.NewGuid();

        var json = DriverNotificationDataBuilder.Build(
            screen: "home",
            @event: "dispatch.offer_new",
            assignmentId: assignmentId);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("presentation").GetString().Should().Be("popup");
        root.GetProperty("popupType").GetString().Should().Be("delivery_offer");
        root.GetProperty("showPopup").GetBoolean().Should().BeTrue();
        root.GetProperty("targetUrl").GetString().Should().Be("/");
        root.GetProperty("category").GetString().Should().Be("dispatch");
    }

    [Fact]
    public void BuildDispatchOfferPushData_ShouldIncludeCompactOfferFieldsForNativeOverlay()
    {
        var orderId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(25);
        var offer = new DriverIncomingOfferDto(
            assignmentId,
            orderId,
            "ORD-TEST-001",
            "Test Vendor",
            "متجر تجريبي",
            "Test Vendor",
            "https://cdn.example/logo.png",
            "Pickup Street 1",
            24.71m,
            46.67m,
            "Customer One",
            "Delivery Street 2",
            24.72m,
            46.68m,
            3.5m,
            "12-17 min",
            18.5m,
            "CashOnDelivery",
            120m,
            120m,
            "TV",
            "CO",
            "Handle with care",
            25,
            [new DriverOfferItemDto("Burger", 2, null)]);

        var json = DriverNotificationDataBuilder.BuildDispatchOfferPushData(
            orderId,
            assignmentId,
            driverId,
            expiresAtUtc,
            offer,
            source: "unit_test");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("vendorName").GetString().Should().Be("متجر تجريبي");
        root.GetProperty("payout").GetDecimal().Should().Be(18.5m);
        root.GetProperty("deliveryFee").GetDecimal().Should().Be(18.5m);
        root.GetProperty("estimatedDistanceKm").GetDecimal().Should().Be(3.5m);
        root.GetProperty("countdownSeconds").GetInt32().Should().Be(25);
        root.GetProperty("itemsCount").GetInt32().Should().Be(1);
        root.TryGetProperty("orderItems", out _).Should().BeFalse();
        root.TryGetProperty("pickupAddress", out _).Should().BeFalse();
        root.TryGetProperty("deliveryAddress", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildDispatchOfferPushData_ShouldStayWithinOneSignalDataLimit()
    {
        var orderId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(45);
        var longText = new string('x', 512);
        var items = Enumerable.Range(0, 20)
            .Select(index => new DriverOfferItemDto($"Product {index} {longText}", 3, longText))
            .ToArray();

        var offer = new DriverIncomingOfferDto(
            assignmentId,
            orderId,
            "ORD-HEAVY-001",
            longText,
            longText,
            longText,
            $"https://cdn.example/{longText}.png",
            longText,
            24.71m,
            46.67m,
            longText,
            longText,
            24.72m,
            46.68m,
            12.5m,
            "45-60 min",
            99.5m,
            "CashOnDelivery",
            999m,
            999m,
            "TV",
            "CO",
            longText,
            45,
            items);

        var json = DriverNotificationDataBuilder.BuildDispatchOfferPushData(
            orderId,
            assignmentId,
            driverId,
            expiresAtUtc,
            offer,
            source: "unit_test");

        Encoding.UTF8.GetByteCount(json).Should().BeLessThan(DriverNotificationDataBuilder.OneSignalMergedPayloadBudgetBytes);
        DriverNotificationDataBuilder.EstimateOneSignalEnvelopeSize(json)
            .Should().BeLessThan(DriverNotificationDataBuilder.OneSignalMaxDataBytes);
    }
}
