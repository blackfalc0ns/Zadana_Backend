using System.Text.Json;
using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverNotificationDataBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Build(
        string screen,
        string @event,
        Guid? orderId = null,
        Guid? assignmentId = null,
        Guid? supportCaseId = null,
        Guid? withdrawalId = null,
        Guid? driverId = null,
        object? extra = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["screen"] = screen,
            ["event"] = @event,
            ["eventName"] = @event,
            ["targetUrl"] = ResolveTargetUrl(screen, orderId, assignmentId, supportCaseId, withdrawalId, driverId),
            ["category"] = ResolveCategory(screen, @event),
            ["presentation"] = "popup",
            ["popupType"] = ResolvePopupType(screen, @event),
            ["showPopup"] = true,
            ["orderId"] = orderId,
            ["assignmentId"] = assignmentId,
            ["supportCaseId"] = supportCaseId,
            ["withdrawalId"] = withdrawalId
        };

        if (supportCaseId.HasValue)
        {
            data["caseId"] = supportCaseId.Value;
        }

        if (driverId.HasValue)
        {
            data["driverId"] = driverId.Value;
        }

        if (extra is not null)
        {
            foreach (var property in JsonSerializer.SerializeToElement(extra, JsonOptions).EnumerateObject())
            {
                data[property.Name] = Deserialize(property.Value);
            }
        }

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static string BuildDispatchOfferInboxData(
        Guid orderId,
        Guid assignmentId,
        Guid driverId,
        DateTime expiresAtUtc,
        object currentOffer,
        string? source = null) =>
        Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: new
            {
                target = "driver-offer",
                legacyType = "driver-offer",
                category = "dispatch",
                action = "offer_new",
                presentation = "popup",
                popupType = "delivery_offer",
                showPopup = true,
                eventName = "dispatch.offer_new",
                source,
                expiresAtUtc,
                currentOffer
            });

    /// <summary>
    /// Push payload for delivery offers. Includes flat offer fields for native overlay
    /// when the app is killed; keep an eye on provider data size limits.
    /// </summary>
    public static string BuildDispatchOfferPushData(
        Guid orderId,
        Guid assignmentId,
        Guid driverId,
        DateTime expiresAtUtc,
        DriverIncomingOfferDto currentOffer,
        string? source = null)
    {
        var itemsCount = currentOffer.OrderItems?.Count ?? 0;

        return Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: new
            {
                target = "driver-offer",
                legacyType = "driver-offer",
                action = "offer_new",
                eventName = "dispatch.offer_new",
                source,
                expiresAtUtc,
                countdownSeconds = currentOffer.CountdownSeconds,
                itemsCount,
                orderNumber = currentOffer.OrderNumber,
                vendorName = currentOffer.VendorName,
                vendorNameAr = currentOffer.VendorNameAr,
                vendorNameEn = currentOffer.VendorNameEn,
                vendorLogoUrl = currentOffer.VendorLogoUrl,
                pickupAddress = currentOffer.PickupAddress,
                pickupLatitude = currentOffer.PickupLatitude,
                pickupLongitude = currentOffer.PickupLongitude,
                customerName = currentOffer.CustomerName,
                deliveryAddress = currentOffer.DeliveryAddress,
                deliveryLatitude = currentOffer.DeliveryLatitude,
                deliveryLongitude = currentOffer.DeliveryLongitude,
                estimatedDistanceKm = currentOffer.EstimatedDistanceKm,
                estimatedEta = currentOffer.EstimatedEta,
                payout = currentOffer.Payout,
                deliveryFee = currentOffer.Payout,
                paymentMethod = currentOffer.PaymentMethod,
                totalAmount = currentOffer.TotalAmount,
                codAmount = currentOffer.CodAmount,
                vendorInitials = currentOffer.VendorInitials,
                customerInitials = currentOffer.CustomerInitials,
                packageNote = currentOffer.PackageNote,
                orderItems = currentOffer.OrderItems
            });
    }

    private static string ResolveTargetUrl(
        string screen,
        Guid? orderId,
        Guid? assignmentId,
        Guid? supportCaseId,
        Guid? withdrawalId,
        Guid? driverId) =>
        Normalize(screen) switch
        {
            "home" => "/",
            "wallet" => withdrawalId.HasValue ? $"/wallet/withdrawals/{withdrawalId}" : "/wallet",
            "account_status" => "/account-status",
            "support_case_detail" => supportCaseId.HasValue ? $"/support/cases/{supportCaseId}" : "/support",
            "order_detail" or "order_tracking" => orderId.HasValue ? $"/orders/{orderId}" : "/orders",
            "assignment" or "assignment_detail" => assignmentId.HasValue ? $"/assignments/{assignmentId}" : "/assignments",
            _ => "/notifications"
        };

    private static string ResolveCategory(string screen, string @event)
    {
        var normalizedScreen = Normalize(screen);
        var normalizedEvent = Normalize(@event);

        if (normalizedEvent.StartsWith("dispatch.", StringComparison.Ordinal))
        {
            return "dispatch";
        }

        if (normalizedEvent.StartsWith("wallet.", StringComparison.Ordinal) || normalizedScreen == "wallet")
        {
            return "wallet";
        }

        if (normalizedEvent.StartsWith("support.", StringComparison.Ordinal) || normalizedScreen == "support_case_detail")
        {
            return "support";
        }

        if (normalizedEvent.StartsWith("assignment.", StringComparison.Ordinal) ||
            normalizedEvent.StartsWith("order.", StringComparison.Ordinal) ||
            normalizedScreen is "assignment" or "order_detail" or "order_tracking")
        {
            return "assignment";
        }

        return "account";
    }

    private static string ResolvePopupType(string screen, string @event)
    {
        var normalizedScreen = Normalize(screen);
        var normalizedEvent = Normalize(@event);

        if (normalizedEvent.StartsWith("dispatch.offer_new", StringComparison.Ordinal))
        {
            return "delivery_offer";
        }

        if (normalizedEvent.StartsWith("dispatch.", StringComparison.Ordinal))
        {
            return "delivery_offer_update";
        }

        if (normalizedEvent.StartsWith("wallet.", StringComparison.Ordinal) || normalizedScreen == "wallet")
        {
            return "driver_wallet_updated";
        }

        if (normalizedEvent.StartsWith("support.", StringComparison.Ordinal) || normalizedScreen == "support_case_detail")
        {
            return "support_case_status_update";
        }

        if (normalizedEvent.StartsWith("assignment.", StringComparison.Ordinal) ||
            normalizedEvent.StartsWith("order.", StringComparison.Ordinal) ||
            normalizedScreen is "assignment" or "order_detail" or "order_tracking")
        {
            return "driver_assignment_updated";
        }

        return "driver_account_updated";
    }

    private static object? Deserialize(JsonElement element) =>
        JsonSerializer.Deserialize<object>(element.GetRawText(), JsonOptions);

    private static string Normalize(string value) =>
        value.Trim().Replace('_', '-').ToLowerInvariant().Replace('-', '_');
}
