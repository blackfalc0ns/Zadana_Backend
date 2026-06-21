using System.Text;
using System.Text.Json;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverNotificationDataBuilder
{
    /// <summary>
    /// OneSignal rejects push when serialized <c>data</c> exceeds 2048 bytes.
    /// BuildAdditionalData nests the payload and merges the same keys at the top level (~2x size).
    /// </summary>
    public const int OneSignalMaxDataBytes = 2048;

    public const int OneSignalMergedPayloadBudgetBytes = 962;

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

        var serialized = Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: extra);

        if (Encoding.UTF8.GetByteCount(serialized) < OneSignalMergedPayloadBudgetBytes)
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

    private static readonly string[] OfferPushSecondaryKeys =
    [
        "source",
        "vendorNameEn",
        "estimatedDistanceKm",
        "orderNumber",
        "distanceText",
        "customerName",
        "paymentMethod"
    ];

    private static readonly string[] OfferPushEssentialKeys =
    [
        "expiresAtUtc",
        "countdownSeconds",
        "vendorName",
        "pickupAddress",
        "deliveryAddress",
        "distanceKm",
        "payout",
        "codAmount",
        "itemsCount"
    ];

    private static string FitOfferWithinPushBudget(
        Dictionary<string, object?> extra,
        DriverIncomingOfferDto offer,
        Guid orderId,
        Guid assignmentId,
        Guid driverId)
    {
        var fullDeliveryAddress = ResolveOverlayDeliveryAddress(offer);
        var fullPickupAddress = offer.PickupAddress ?? string.Empty;

        var workingExtra = new Dictionary<string, object?>(extra)
        {
            ["deliveryAddress"] = fullDeliveryAddress,
            ["pickupAddress"] = fullPickupAddress
        };

        for (var removalCount = 0; removalCount <= OfferPushSecondaryKeys.Length; removalCount++)
        {
            if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var candidate))
            {
                return candidate;
            }

            if (removalCount < OfferPushSecondaryKeys.Length)
            {
                workingExtra.Remove(OfferPushSecondaryKeys[removalCount]);
            }
        }

        var essentialWithBothAddresses = BuildEssentialOfferExtra(workingExtra, fullDeliveryAddress, fullPickupAddress);
        if (TryBuildOfferPushPayload(essentialWithBothAddresses, orderId, assignmentId, driverId, out var essentialBothCandidate))
        {
            return essentialBothCandidate;
        }

        var vendorName = workingExtra.GetValueOrDefault("vendorName")?.ToString() ?? string.Empty;
        foreach (var maxElements in new[] { 16, 12, 8 })
        {
            workingExtra["vendorName"] = TruncateByTextElements(vendorName, maxElements);
            if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var candidate))
            {
                return candidate;
            }
        }

        workingExtra["deliveryAddress"] = fullDeliveryAddress;
        workingExtra.Remove("pickupAddress");
        workingExtra.Remove("totalAmount");
        if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var deliveryFirstCandidate))
        {
            return deliveryFirstCandidate;
        }

        var essentialDeliveryOnly = BuildEssentialOfferExtra(workingExtra, fullDeliveryAddress, pickupAddress: null);
        if (TryBuildOfferPushPayload(essentialDeliveryOnly, orderId, assignmentId, driverId, out var essentialDeliveryCandidate))
        {
            return essentialDeliveryCandidate;
        }

        workingExtra["pickupAddress"] = fullPickupAddress;
        if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var bothAddressesCandidate))
        {
            return bothAddressesCandidate;
        }

        if (IsCoordinateAddress(fullDeliveryAddress))
        {
            return FitCompactDeliveryAddressWithinPushBudget(
                workingExtra,
                fullDeliveryAddress,
                orderId,
                assignmentId,
                driverId);
        }

        workingExtra["pickupAddress"] = TruncateByTextElements(fullPickupAddress, 20);
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

        var fittedPickup = TryFitPickupAddress(
            workingExtra,
            fullPickupAddress,
            TruncateByTextElements(fullDeliveryAddress, 20),
            orderId,
            assignmentId,
            driverId);

        if (fittedPickup is not null)
        {
            return fittedPickup;
        }

        workingExtra["deliveryAddress"] = TruncateByTextElements(fullDeliveryAddress, 20);
        workingExtra["pickupAddress"] = TruncateByTextElements(fullPickupAddress, 20);
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

        return Encoding.UTF8.GetByteCount(payload) < OneSignalMergedPayloadBudgetBytes;
    }

    private static Dictionary<string, object?> BuildEssentialOfferExtra(
        Dictionary<string, object?> extra,
        string fullDeliveryAddress,
        string? pickupAddress)
    {
        var essential = OfferPushEssentialKeys
            .Where(extra.ContainsKey)
            .ToDictionary(key => key, key => extra[key]);

        essential["deliveryAddress"] = fullDeliveryAddress;

        if (string.IsNullOrWhiteSpace(pickupAddress))
        {
            essential.Remove("pickupAddress");
        }
        else
        {
            essential["pickupAddress"] = pickupAddress;
        }

        if (essential.TryGetValue("vendorName", out var vendorName))
        {
            essential["vendorName"] = TruncateByTextElements(vendorName?.ToString() ?? string.Empty, 10);
        }

        return essential;
    }

    private static string? TryFitPickupAddress(
        Dictionary<string, object?> extra,
        string pickupAddress,
        string deliveryAddress,
        Guid orderId,
        Guid assignmentId,
        Guid driverId)
    {
        var normalizedPickup = pickupAddress.Trim();
        if (string.IsNullOrEmpty(normalizedPickup))
        {
            return null;
        }

        var pickupInfo = new System.Globalization.StringInfo(normalizedPickup);
        var textElements = System.Globalization.StringInfo.ParseCombiningCharacters(normalizedPickup);
        var low = 1;
        var high = textElements.Length;
        string? best = null;

        while (low <= high)
        {
            var count = low + ((high - low) / 2);
            var candidateExtra = new Dictionary<string, object?>(extra)
            {
                ["deliveryAddress"] = deliveryAddress,
                ["pickupAddress"] = pickupInfo.SubstringByTextElements(0, count)
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

    private static string? TryFitDeliveryAddress(
        Dictionary<string, object?> extra,
        string deliveryAddress,
        Guid orderId,
        Guid assignmentId,
        Guid driverId)
    {
        var normalizedAddress = deliveryAddress.Trim();
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

    private static string FitCompactDeliveryAddressWithinPushBudget(
        Dictionary<string, object?> extra,
        string deliveryAddress,
        Guid orderId,
        Guid assignmentId,
        Guid driverId)
    {
        var workingExtra = new Dictionary<string, object?>(extra);

        for (var removalCount = 0; removalCount <= OfferPushSecondaryKeys.Length; removalCount++)
        {
            workingExtra["deliveryAddress"] = deliveryAddress;
            if (TryBuildOfferPushPayload(workingExtra, orderId, assignmentId, driverId, out var candidate))
            {
                return candidate;
            }

            if (removalCount < OfferPushSecondaryKeys.Length)
            {
                workingExtra.Remove(OfferPushSecondaryKeys[removalCount]);
            }
        }

        workingExtra["deliveryAddress"] = deliveryAddress;
        return Build(
            screen: "home",
            @event: "dispatch.offer_new",
            orderId: orderId,
            assignmentId: assignmentId,
            driverId: driverId,
            extra: workingExtra);
    }

    private static bool IsCoordinateAddress(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
               decimal.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) &&
               decimal.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
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
            ["expiresAtUtc"] = expiresAtUtc
        };

        if (!compact)
        {
            data["target"] = "driver-offer";
            data["action"] = "offer_new";
        }

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
        if (!compact)
        {
            data["vendorNameEn"] = offer.VendorNameEn;
        }

        data["pickupAddress"] = offer.PickupAddress ?? string.Empty;
        data["deliveryAddress"] = ResolveOverlayDeliveryAddress(offer);
        data["customerName"] = compact ? Truncate(offer.CustomerName, 16) : offer.CustomerName;
        data["estimatedDistanceKm"] = offer.EstimatedDistanceKm;
        data["distanceKm"] = offer.EstimatedDistanceKm;
        data["distanceText"] = BuildDistanceText(offer.EstimatedDistanceKm);
        data["payout"] = offer.Payout;
        data["paymentMethod"] = compact ? Truncate(offer.PaymentMethod, 12) : offer.PaymentMethod;
        if (!compact)
        {
            data["totalAmount"] = offer.TotalAmount;
        }

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
