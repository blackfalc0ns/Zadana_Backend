using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Orders.Requests;

public record GetCheckoutSummaryResponse(
    [property: JsonPropertyName("cart")] CheckoutCartResponse Cart,
    [property: JsonPropertyName("selected_address")] CheckoutSelectedAddressResponse? SelectedAddress,
    [property: JsonPropertyName("delivery_slots")] List<CheckoutDeliverySlotResponse> DeliverySlots,
    [property: JsonPropertyName("payment_methods")] List<CheckoutPaymentMethodResponse> PaymentMethods,
    [property: JsonPropertyName("promo_code")] CheckoutPromoCodeResponse? PromoCode,
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

public record PlaceOrderRequest(
    [property: JsonPropertyName("address_id")] Guid AddressId,
    [property: JsonPropertyName("delivery_slot_id")] string? DeliverySlotId,
    [property: JsonPropertyName("payment_method")] string PaymentMethod,
    [property: JsonPropertyName("promo_code")] string? PromoCode,
    [property: JsonPropertyName("notes")] string? Notes);

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

public record LegacyPlaceCheckoutOrderRequest(
    [property: JsonPropertyName("vendor_id")] Guid VendorId,
    [property: JsonPropertyName("address_id")] Guid AddressId,
    [property: JsonPropertyName("payment_method_id")] string PaymentMethodId,
    [property: JsonPropertyName("promo_code")] string? PromoCode,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("vendor_branch_id")] Guid? VendorBranchId);
