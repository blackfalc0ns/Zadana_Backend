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
        root.GetProperty("pickupAddress").GetString().Should().Be("Pickup Street 1");
        root.GetProperty("deliveryAddress").GetString().Should().Be("Delivery Street 2");
        root.GetProperty("customerName").GetString().Should().Be("Customer One");
        root.GetProperty("payout").GetDecimal().Should().Be(18.5m);
        root.GetProperty("codAmount").GetDecimal().Should().Be(120m);
        root.TryGetProperty("totalAmount", out _).Should().BeFalse();
        root.GetProperty("estimatedDistanceKm").GetDecimal().Should().Be(3.5m);
        root.GetProperty("distanceKm").GetDecimal().Should().Be(3.5m);
        root.GetProperty("distanceText").GetString().Should().Be("3.5 km");
        root.GetProperty("countdownSeconds").GetInt32().Should().Be(25);
        DateTimeOffset.Parse(root.GetProperty("expiresAtUtc").GetString()!)
            .Offset.Should().Be(TimeSpan.FromHours(3));
        root.GetProperty("itemsCount").GetInt32().Should().Be(1);
        root.TryGetProperty("orderItems", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithLocalizedText_ShouldIncludeArabicAndEnglishCopyInPayload()
    {
        var json = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.document_approved",
            driverId: Guid.NewGuid(),
            titleAr: "تمت الموافقة على رخصة القيادة",
            titleEn: "Driver license approved",
            bodyAr: "تمت مراجعة رخصة القيادة والموافقة عليه.",
            bodyEn: "Your driver license was reviewed and approved.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("titleAr").GetString().Should().Be("تمت الموافقة على رخصة القيادة");
        root.GetProperty("titleEn").GetString().Should().Be("Driver license approved");
        root.GetProperty("bodyAr").GetString().Should().Be("تمت مراجعة رخصة القيادة والموافقة عليه.");
        root.GetProperty("bodyEn").GetString().Should().Be("Your driver license was reviewed and approved.");
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

    [Fact]
    public void BuildDispatchOfferPushData_WhenArabicPayloadIsLarge_ShouldPreserveDeliveryAddress()
    {
        var offer = new DriverIncomingOfferDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ORD-20260620-D702B831",
            "Hope Grocery",
            "بقالة الأمل",
            "Hope Grocery",
            null,
            "سشيشسيشيسشيشسسشيشي",
            24.71m,
            46.67m,
            "ahmed",
            "العدامة، الدمام، المملكة العربية السعودية، مبنى 15، الدور الثالث، شقة 8",
            26.42m,
            50.08m,
            13.49m,
            "54-59 min",
            47.45m,
            "CashOnDelivery",
            99.6m,
            99.6m,
            "HG",
            "AH",
            null,
            60,
            [new DriverOfferItemDto("منتج تجريبي", 1, null)]);

        var json = DriverNotificationDataBuilder.BuildDispatchOfferPushData(
            offer.OrderId,
            offer.AssignmentId,
            Guid.NewGuid(),
            DateTime.UtcNow.AddSeconds(60),
            offer);

        using var document = JsonDocument.Parse(json);
        var deliveryAddress = document.RootElement.GetProperty("deliveryAddress").GetString();

        deliveryAddress.Should().Be("العدامة، الدمام، المملكة العربية السعودية، مبنى 15، الدور الثالث، شقة 8");
        if (document.RootElement.TryGetProperty("pickupAddress", out var pickupAddress))
        {
            pickupAddress.GetString().Should().Be("سشيشسيشيسشيشسسشيشي");
        }
        DriverNotificationDataBuilder.EstimateOneSignalEnvelopeSize(json)
            .Should().BeLessThan(DriverNotificationDataBuilder.OneSignalMaxDataBytes);
    }

    [Fact]
    public void BuildDispatchOfferPushData_ShouldFallbackToCoordinatesWhenDeliveryAddressTextIsMissing()
    {
        var offer = new DriverIncomingOfferDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ORD-COORDS-001",
            "Hope Grocery",
            "بقالة الأمل",
            "Hope Grocery",
            null,
            "سشيشسيشيسشيشسسشيشي",
            24.71m,
            46.67m,
            "ahmed",
            string.Empty,
            26.42m,
            50.08m,
            13.49m,
            "54-59 min",
            47.45m,
            "CashOnDelivery",
            99.6m,
            99.6m,
            "HG",
            "AH",
            null,
            60,
            [new DriverOfferItemDto("منتج تجريبي", 1, null)]);

        var json = DriverNotificationDataBuilder.BuildDispatchOfferPushData(
            offer.OrderId,
            offer.AssignmentId,
            Guid.NewGuid(),
            DateTime.UtcNow.AddSeconds(60),
            offer);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("deliveryAddress").GetString()
            .Should().Be("26.42, 50.08");
    }
}
