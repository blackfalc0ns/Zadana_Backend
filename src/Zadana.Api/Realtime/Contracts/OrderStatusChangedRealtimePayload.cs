using System.Text.Json.Serialization;

namespace Zadana.Api.Realtime.Contracts;

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
    [property: JsonPropertyName("newStatusRaw")] string? NewStatusRaw = null);
