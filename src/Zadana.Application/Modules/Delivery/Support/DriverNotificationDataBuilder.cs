using System.Text;
using System.Text.Json;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverNotificationDataBuilder
{
    /// <summary>
    /// OneSignal rejects push when serialized <c>data</c> exceeds 2048 bytes.
    /// Driver delivery offers use flat merged keys only (no duplicate nested payload).
    /// </summary>
    public const int OneSignalMaxDataBytes = 2048;

    public const int OneSignalDriverOfferPayloadBudgetBytes = 1850;

    private const int OneSignalEnvelopeOverheadBytes = 180;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static DriverNotificationDataBuilder()
    {
        JsonOptions.Converters.Add(new SaudiDateTimeJsonConverter());
        JsonOptions.Converters.Add(new SaudiDateTimeOffsetJsonConverter());
    }

    public static string Build(
        string screen,
        string @event,
        Guid? orderId = null,
        Guid? assignmentId = null,
        Guid? supportCaseId = null,
        Guid? withdrawalId = null,
        Guid? driverId = null,
        string? titleAr = null,
        string? titleEn = null,
        string? bodyAr = null,
        string? bodyEn = null,
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

        AppendLocalizedText(data, titleAr, titleEn, bodyAr, bodyEn);

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
    /// Push payload for the native driver offer overlay.
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

        var serialized = Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: extra);

        if (Encoding.UTF8.GetByteCount(serialized) < OneSignalDriverOfferPayloadBudgetBytes)
        {
            return serialized;
        }

        return FitOfferWithinPushBudget(
            extra,
            currentOffer,
            orderId,
            assignmentId,
            driverId);
    }

    private static readonly string[] OfferPushOptionalKeys =
    [
        "source",
        "vendorNameEn",
        "estimatedDistanceKm",
        "orderNumber"
    ];

    private static string FitOfferWithinPushBudget(
        Dictionary<string, object?> extra,
        DriverIncomingOfferDto offer,
        Guid orderId,
        Guid assignmentId,
        Guid driverId)
    {
        var vendorName = FirstNonBlank(offer.VendorNameAr, offer.VendorName, offer.VendorNameEn);
        var workingExtra = new Dictionary<string, object?>(extra)
        {
            ["vendorName"] = vendorName,
            ["pickupAddress"] = offer.PickupAddress ?? string.Empty,
            ["deliveryAddress"] = ResolveOverlayDeliveryAddress(offer),
            ["customerName"] = offer.CustomerName,
            ["paymentMethod"] = offer.PaymentMethod,
            ["distanceText"] = BuildDistanceText(offer.EstimatedDistanceKm),
            ["totalAmount"] = offer.TotalAmount,
            ["codAmount"] = offer.CodAmount,
            ["payout"] = offer.Payout,
            ["distanceKm"] = offer.EstimatedDistanceKm,
            ["countdownSeconds"] = offer.CountdownSeconds,
            ["itemsCount"] = (offer.OrderItems ?? []).Count
        };

        for (var removalCount = 0; removalCount <= OfferPushOptionalKeys.Length; removalCount++)
        {
            if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var candidate))
            {
                return candidate;
            }

            if (removalCount < OfferPushOptionalKeys.Length)
            {
                workingExtra.Remove(OfferPushOptionalKeys[removalCount]);
            }
        }

        var fullDeliveryAddress = ResolveOverlayDeliveryAddress(offer);
        if (IsCoordinateAddress(fullDeliveryAddress))
        {
            workingExtra["deliveryAddress"] = fullDeliveryAddress;
            if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var coordinateCandidate))
            {
                return coordinateCandidate;
            }
        }

        var fittedDelivery = TryFitDeliveryAddress(
            workingExtra,
            fullDeliveryAddress,
            orderId,
            assignmentId,
            driverId);

        if (fittedDelivery is not null)
        {
            return fittedDelivery;
        }

        workingExtra["deliveryAddress"] = TruncateByTextElements(fullDeliveryAddress, 20);
        workingExtra["pickupAddress"] = TruncateByTextElements(offer.PickupAddress ?? string.Empty, 20);
        return Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: workingExtra);
    }

    private static bool TryBuildOfferPushPayload(
        Dictionary<string, object?> extra,
        Guid orderId,
        Guid assignmentId,
        Guid driverId,
        out string payload)
    {
        payload = Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: extra);

        return Encoding.UTF8.GetByteCount(payload) < OneSignalDriverOfferPayloadBudgetBytes;
    }

    private static string? TryFitDeliveryAddress(
        Dictionary<string, object?> extra,
        string deliveryAddress,
        Guid orderId,
        Guid assignmentId,
        Guid driverId)
    {
        var normalizedAddress = deliveryAddress.Trim();
        if (string.IsNullOrEmpty(normalizedAddress))
        {
            return null;
        }

        var textElements = System.Globalization.StringInfo.ParseCombiningCharacters(normalizedAddress);
        var addressInfo = new System.Globalization.StringInfo(normalizedAddress);
        var low = 1;
        var high = textElements.Length;
        string? best = null;

        while (low <= high)
        {
            var count = low + ((high - low) / 2);
            var candidateExtra = new Dictionary<string, object?>(extra)
            {
                ["deliveryAddress"] = addressInfo.SubstringByTextElements(0, count)
            };

            if (TryBuildOfferPushPayload(candidateExtra, orderId, assignmentId, driverId, out var candidate))
            {
                best = candidate;
                low = count + 1;
            }
            else
            {
                high = count - 1;
            }
        }

        return best;
    }

    private static bool IsCoordinateAddress(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
               decimal.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) &&
               decimal.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Approximates OneSignal envelope size for driver offer pushes (flat merged data).
    /// </summary>
    public static int EstimateOneSignalEnvelopeSize(string pushPayloadJson) =>
        EstimateDriverOfferEnvelopeSize(pushPayloadJson);

    public static int EstimateDriverOfferEnvelopeSize(string pushPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(pushPayloadJson))
        {
            return 0;
        }

        return Encoding.UTF8.GetByteCount(pushPayloadJson) + OneSignalEnvelopeOverheadBytes;
    }

    /// <summary>
    /// Legacy estimate when payload is nested and merged at the top level.
    /// </summary>
    public static int EstimateDuplicatedPayloadEnvelopeSize(string pushPayloadJson)
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
        data["vendorName"] = vendorName;
        data["vendorNameEn"] = offer.VendorNameEn;
        data["pickupAddress"] = offer.PickupAddress ?? string.Empty;
        data["deliveryAddress"] = ResolveOverlayDeliveryAddress(offer);
        data["customerName"] = offer.CustomerName;
        data["estimatedDistanceKm"] = offer.EstimatedDistanceKm;
        data["distanceKm"] = offer.EstimatedDistanceKm;
        data["distanceText"] = BuildDistanceText(offer.EstimatedDistanceKm);
        data["payout"] = offer.Payout;
        data["paymentMethod"] = offer.PaymentMethod;
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

    private static string ResolveOverlayDeliveryAddress(DriverIncomingOfferDto offer)
    {
        if (!string.IsNullOrWhiteSpace(offer.DeliveryAddress))
        {
            return offer.DeliveryAddress.Trim();
        }

        if (offer.DeliveryLatitude is decimal latitude && offer.DeliveryLongitude is decimal longitude)
        {
            return $"{latitude:0.######}, {longitude:0.######}";
        }

        return string.Empty;
    }

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

    private static string TruncateByTextElements(string value, int maxTextElements)
    {
        var addressInfo = new System.Globalization.StringInfo(value);
        return addressInfo.LengthInTextElements <= maxTextElements
            ? value
            : addressInfo.SubstringByTextElements(0, maxTextElements);
    }

    private static void AppendLocalizedText(
        Dictionary<string, object?> data,
        string? titleAr,
        string? titleEn,
        string? bodyAr,
        string? bodyEn)
    {
        if (!string.IsNullOrWhiteSpace(titleAr))
        {
            data["titleAr"] = titleAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(titleEn))
        {
            data["titleEn"] = titleEn.Trim();
        }

        if (!string.IsNullOrWhiteSpace(bodyAr))
        {
            data["bodyAr"] = bodyAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(bodyEn))
        {
            data["bodyEn"] = bodyEn.Trim();
        }
    }
}
