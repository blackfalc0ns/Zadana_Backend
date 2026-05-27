using System.Text.Json.Serialization;

namespace Zadana.Api.Realtime.Contracts;

public sealed record DeliveryOfferRealtimePayload(
    [property: JsonPropertyName("assignmentId")] Guid AssignmentId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("orderNumber")] string OrderNumber,
    [property: JsonPropertyName("vendorName")] string VendorName,
    [property: JsonPropertyName("deliveryFee")] decimal DeliveryFee,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("codAmount")] decimal CodAmount,
    [property: JsonPropertyName("paymentMethod")] string PaymentMethod,
    [property: JsonPropertyName("countdownSeconds")] int CountdownSeconds,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("presentation")] string Presentation = "popup",
    [property: JsonPropertyName("popupType")] string PopupType = "delivery_offer",
    [property: JsonPropertyName("showPopup")] bool ShowPopup = true,
    [property: JsonPropertyName("eventName")] string EventName = "dispatch.offer_new");
