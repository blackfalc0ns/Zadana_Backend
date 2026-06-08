using System.Text.Json.Serialization;
using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Api.Realtime.Contracts;

public sealed record DeliveryOfferRealtimePayload(
    [property: JsonPropertyName("currentOffer")] DriverIncomingOfferDto CurrentOffer,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("presentation")] string Presentation = "popup",
    [property: JsonPropertyName("popupType")] string PopupType = "delivery_offer",
    [property: JsonPropertyName("showPopup")] bool ShowPopup = true,
    [property: JsonPropertyName("eventName")] string EventName = "dispatch.offer_new");
