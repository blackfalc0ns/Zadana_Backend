using System.Text.Json.Serialization;

namespace Zadana.Api.Realtime.Contracts;

public sealed record OrderPickupBranchRealtimePayload(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("hoursToday")] string? HoursToday);

public sealed record OrderStatusChangedRealtimePayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("orderNumber")] string OrderNumber,
    [property: JsonPropertyName("vendorId")] Guid VendorId,
    [property: JsonPropertyName("oldStatus")] string OldStatus,
    [property: JsonPropertyName("newStatus")] string NewStatus,
    [property: JsonPropertyName("actorRole")] string? ActorRole,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("targetUrl")] string TargetUrl,
    [property: JsonPropertyName("changedAtUtc")] DateTime ChangedAtUtc,
    [property: JsonPropertyName("presentation")] string Presentation = "popup",
    [property: JsonPropertyName("popupType")] string PopupType = "order_status_changed",
    [property: JsonPropertyName("showPopup")] bool ShowPopup = true,
    [property: JsonPropertyName("oldStatusRaw")] string? OldStatusRaw = null,
    [property: JsonPropertyName("newStatusRaw")] string? NewStatusRaw = null,
    [property: JsonPropertyName("fulfillmentType")] string? FulfillmentType = null,
    [property: JsonPropertyName("pickupOtpCode")] string? PickupOtpCode = null,
    [property: JsonPropertyName("pickupOtpExpiresAtUtc")] DateTime? PickupOtpExpiresAtUtc = null,
    [property: JsonPropertyName("pickupNoShowDeadlineUtc")] DateTime? PickupNoShowDeadlineUtc = null,
    [property: JsonPropertyName("pickupBranch")] OrderPickupBranchRealtimePayload? PickupBranch = null);
