using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Orders.Requests;

public record PlaceCheckoutOrderRequest(
    [property: JsonPropertyName("vendor_id")] Guid VendorId,
    [property: JsonPropertyName("address_id")] Guid AddressId,
    [property: JsonPropertyName("payment_method_id")] string PaymentMethodId,
    [property: JsonPropertyName("promo_code")] string? PromoCode,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("vendor_branch_id")] Guid? VendorBranchId);

public record PlaceCheckoutOrderResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("order")] PlaceCheckoutOrderSummary Order,
    [property: JsonPropertyName("payment")] PlaceCheckoutPaymentSummary Payment);

public record PlaceCheckoutOrderSummary(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("payment_method_id")] string PaymentMethodId);

public record PlaceCheckoutPaymentSummary(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("iframe_url")] string IframeUrl,
    [property: JsonPropertyName("provider_reference")] string ProviderReference);
