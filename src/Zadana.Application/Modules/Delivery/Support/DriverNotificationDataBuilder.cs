using System.Text;
using System.Text.Json;
using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverNotificationDataBuilder
{
    /// <summary>
    /// OneSignal rejects push when serialized <c>data</c> exceeds 2048 bytes.
    /// BuildAdditionalData nests the payload and merges the same keys at the top level (~2x size).
    /// </summary>
    public const int OneSignalMaxDataBytes = 2048;

    public const int OneSignalMergedPayloadBudgetBytes = 950;

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
        string? source = null)
    {
        var extra = BuildOfferOverlayData(
            currentOffer,
            expiresAtUtc,
            source,
            includeFullItems: true);

        extra["currentOffer"] = currentOffer;

        return Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: extra);
    }

    /// <summary>
    /// Compact push payload for delivery offers when the app is killed.
    /// Full offer details stay in inbox + SignalR; mobile should refresh home on tap.
    /// </summary>
    public static string BuildDispatchOfferPushData(
        Guid orderId,
        Guid assignmentId,
        Guid driverId,
        DateTime expiresAtUtc,
        DriverIncomingOfferDto currentOffer,
        string? source = null)
    {
        var extra = BuildOfferOverlayData(
            currentOffer,
            expiresAtUtc,
            source,
            includeFullItems: false,
            compact: true);

        return Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: extra);
    }

    /// <summary>
    /// Approximates OneSignal envelope size after nested payload + top-level merge.
    /// </summary>
    public static int EstimateOneSignalEnvelopeSize(string pushPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(pushPayloadJson))
        {
            return 0;
        }

        var payloadBytes = Encoding.UTF8.GetByteCount(pushPayloadJson);
        return payloadBytes * 2 + 120;
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

    private static Dictionary<string, object?> BuildOfferOverlayData(
        object currentOffer,
        DateTime expiresAtUtc,
        string? source,
        bool includeFullItems,
        bool compact = false)
    {
        var data = new Dictionary<string, object?>
        {
            ["target"] = "driver-offer",
            ["action"] = "offer_new",
            ["expiresAtUtc"] = expiresAtUtc
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            data["source"] = source;
        }

        if (currentOffer is not DriverIncomingOfferDto offer)
        {
            return data;
        }

        var items = offer.OrderItems ?? [];
        var vendorName = FirstNonBlank(offer.VendorNameAr, offer.VendorName, offer.VendorNameEn);

        data["countdownSeconds"] = offer.CountdownSeconds;
        data["itemsCount"] = items.Count;
        data["orderNumber"] = compact ? Truncate(offer.OrderNumber, 12) : offer.OrderNumber;
        data["vendorName"] = compact ? Truncate(vendorName, 24) : vendorName;
        data["vendorNameEn"] = compact ? Truncate(offer.VendorNameEn, 24) : offer.VendorNameEn;
        data["pickupAddress"] = compact ? Truncate(offer.PickupAddress, 20) : offer.PickupAddress;
        data["deliveryAddress"] = compact ? Truncate(offer.DeliveryAddress, 20) : offer.DeliveryAddress;
        data["customerName"] = compact ? Truncate(offer.CustomerName, 16) : offer.CustomerName;
        data["estimatedDistanceKm"] = offer.EstimatedDistanceKm;
        data["distanceKm"] = offer.EstimatedDistanceKm;
        data["distanceText"] = BuildDistanceText(offer.EstimatedDistanceKm);
        data["payout"] = offer.Payout;
        data["paymentMethod"] = compact ? Truncate(offer.PaymentMethod, 12) : offer.PaymentMethod;
        data["totalAmount"] = offer.TotalAmount;
        data["codAmount"] = offer.CodAmount;

        if (!compact)
        {
            data["estimatedEta"] = offer.EstimatedEta;
            data["deliveryFee"] = offer.Payout;
            data["vendorNameAr"] = offer.VendorNameAr;
            data["vendorLogoUrl"] = offer.VendorLogoUrl;
            data["pickupLatitude"] = offer.PickupLatitude;
            data["pickupLongitude"] = offer.PickupLongitude;
            data["deliveryLatitude"] = offer.DeliveryLatitude;
            data["deliveryLongitude"] = offer.DeliveryLongitude;
            data["vendorInitials"] = offer.VendorInitials;
            data["customerInitials"] = offer.CustomerInitials;
            data["packageNote"] = offer.PackageNote;
        }

        if (includeFullItems)
        {
            data["orderItems"] = items;
        }

        return data;
    }

    private static string BuildDistanceText(decimal distanceKm) =>
        $"{distanceKm:0.##} km";

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }
}
