using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Orders.Requests;

public record GetCheckoutSummaryResponse(
    [property: JsonPropertyName("cart")] CheckoutCartResponse Cart,
    [property: JsonPropertyName("address_id")] Guid? AddressId,
    [property: JsonPropertyName("selected_address")] CheckoutSelectedAddressResponse? SelectedAddress,
    [property: JsonPropertyName("delivery_slots")] List<CheckoutDeliverySlotResponse> DeliverySlots,
    [property: JsonPropertyName("payment_methods")] List<CheckoutPaymentMethodResponse> PaymentMethods,
    [property: JsonPropertyName("promo_code")] CheckoutPromoCodeResponse? PromoCode,
    [property: JsonPropertyName("delivery_quote")] CheckoutDeliveryQuoteResponse DeliveryQuote,
    [property: JsonPropertyName("shipping_breakdown")] List<CheckoutShippingBreakdownLineResponse> ShippingBreakdown,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("summary")] CheckoutSummaryTotalsResponse Summary);

public record CheckoutCartResponse(
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("total_quantity")] int TotalQuantity,
    [property: JsonPropertyName("items")] List<CheckoutCartItemResponse> Items);

public record CheckoutCartItemResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("product_id")] Guid ProductId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("total_price")] decimal TotalPrice);

public record CheckoutSelectedAddressResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("address_line")] string AddressLine,
    [property: JsonPropertyName("is_default")] bool IsDefault);

public record CheckoutDeliverySlotResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("start_at")] DateTime StartAt,
    [property: JsonPropertyName("end_at")] DateTime EndAt,
    [property: JsonPropertyName("is_available")] bool IsAvailable,
    [property: JsonPropertyName("is_selected")] bool IsSelected);

public record CheckoutPaymentMethodResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("is_available")] bool IsAvailable,
    [property: JsonPropertyName("is_default")] bool IsDefault);

public record CheckoutPromoCodeResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("discount_type")] string DiscountType,
    [property: JsonPropertyName("discount_value")] decimal DiscountValue,
    [property: JsonPropertyName("discount_amount")] decimal DiscountAmount);

public record CheckoutDeliveryQuoteResponse(
    [property: JsonPropertyName("distance_km")] decimal DistanceKm,
    [property: JsonPropertyName("base_fee")] decimal BaseFee,
    [property: JsonPropertyName("distance_fee")] decimal DistanceFee,
    [property: JsonPropertyName("surge_fee")] decimal SurgeFee,
    [property: JsonPropertyName("total_fee")] decimal TotalFee,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("rule_label")] string RuleLabel);

public record CheckoutShippingBreakdownLineResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("amount")] decimal Amount);

public record CheckoutSummaryTotalsResponse(
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("shipping_cost")] decimal ShippingCost,
    [property: JsonPropertyName("discount")] decimal Discount,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("currency")] string Currency);

public record ApplyCheckoutPromoCodeRequest(
    [property: JsonPropertyName("code")] string Code);

public record ApplyCheckoutPromoCodeResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("promo_code")] CheckoutPromoCodeResponse PromoCode,
    [property: JsonPropertyName("summary")] CheckoutSummaryTotalsResponse Summary);

public record RemoveCheckoutPromoCodeResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("summary")] CheckoutSummaryTotalsResponse Summary);

public class PlaceOrderRequest
{
    [JsonPropertyName("vendor_id")]
    public Guid? VendorId { get; init; }

    [JsonPropertyName("address_id")]
    public Guid AddressId { get; init; }

    [JsonPropertyName("delivery_slot_id")]
    public string? DeliverySlotId { get; init; }

    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; init; }

    [JsonPropertyName("promo_code")]
    public string? PromoCode { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    [JsonIgnore]
    public Guid? EffectiveVendorId => VendorId ?? ReadGuid("vendorId");

    [JsonIgnore]
    public Guid EffectiveAddressId => AddressId != Guid.Empty ? AddressId : ReadGuid("addressId") ?? Guid.Empty;

    [JsonIgnore]
    public string? EffectiveDeliverySlotId => DeliverySlotId ?? ReadString("deliverySlotId");

    [JsonIgnore]
    public string EffectivePaymentMethod => NormalizePaymentMethod(PaymentMethod ?? ReadString("paymentMethod"));

    [JsonIgnore]
    public string? EffectivePromoCode => PromoCode ?? ReadString("promoCode");

    [JsonIgnore]
    public string? EffectiveNotes => Notes ?? ReadString("note") ?? ReadString("notes");

    private Guid? ReadGuid(string propertyName)
    {
        if (ExtensionData is null || !ExtensionData.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(value.GetString(), out var parsedGuid))
        {
            return parsedGuid;
        }

        return null;
    }

    private string? ReadString(string propertyName)
    {
        if (ExtensionData is null || !ExtensionData.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "cash_on_delivery" or "cashondelivery" or "cod" => "cash",
            "bank_transfer" or "banktransfer" => "bank",
            "credit_card" or "creditcard" or "debit_card" or "debitcard" => "card",
            "applepay" => "apple_pay",
            _ => normalized ?? string.Empty
        };
    }
}

public record PlaceOrderResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("order")] PlacedOrderSummaryResponse Order,
    [property: JsonPropertyName("payment")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CheckoutOrderPaymentResponse? Payment);

public record PlacedOrderSummaryResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("payment_method")] string PaymentMethod,
    [property: JsonPropertyName("payment_status")] string PaymentStatus,
    [property: JsonPropertyName("total_price")] decimal TotalPrice);

public record CheckoutOrderPaymentResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("iframe_url")] string IframeUrl,
    [property: JsonPropertyName("provider_reference")] string ProviderReference);
