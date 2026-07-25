using System.Text.Json;
using System.Text.Json.Serialization;
using OrderFulfillmentType = Zadana.Domain.Modules.Orders.Enums.FulfillmentType;

namespace Zadana.Api.Modules.Orders.Requests;

public record GetCheckoutSummaryResponse(
    [property: JsonPropertyName("cart")] CheckoutCartResponse Cart,
    [property: JsonPropertyName("address_id")] Guid? AddressId,
    [property: JsonPropertyName("selected_address")] CheckoutSelectedAddressResponse? SelectedAddress,
    [property: JsonPropertyName("available_addresses")] List<CheckoutSelectedAddressResponse> AvailableAddresses,
    [property: JsonPropertyName("delivery_slots")] List<CheckoutDeliverySlotResponse> DeliverySlots,
    [property: JsonPropertyName("payment_methods")] List<CheckoutPaymentMethodResponse> PaymentMethods,
    [property: JsonPropertyName("promo_code")] CheckoutPromoCodeResponse? PromoCode,
    [property: JsonPropertyName("delivery_check")] CheckoutDeliveryCheckResponse DeliveryCheck,
    [property: JsonPropertyName("estimated_delivery_window")] CheckoutEstimatedDeliveryWindowResponse? EstimatedDeliveryWindow,
    [property: JsonPropertyName("delivery_quote")] CheckoutDeliveryQuoteResponse? DeliveryQuote,
    [property: JsonPropertyName("delivery_breakdown")] CheckoutDeliveryBreakdownResponse? DeliveryBreakdown,
    [property: JsonPropertyName("shipping_breakdown")] List<CheckoutShippingBreakdownLineResponse> ShippingBreakdown,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("summary")] CheckoutSummaryTotalsResponse Summary,
    [property: JsonPropertyName("fulfillment_type")] string FulfillmentType = "delivery",
    [property: JsonPropertyName("pickup_branch")] CheckoutPickupBranchResponse? PickupBranch = null);

public record CheckoutPickupBranchResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("address_line")] string AddressLine,
    [property: JsonPropertyName("city")] string City);

public record CheckoutConfigResponse(
    [property: JsonPropertyName("delivery_enabled")] bool DeliveryEnabled,
    [property: JsonPropertyName("pickup_enabled")] bool PickupEnabled,
    [property: JsonPropertyName("pickup_cash_on_pickup_enabled")] bool PickupCashOnPickupEnabled,
    [property: JsonPropertyName("allowed_payments_for_pickup")] IReadOnlyList<string> AllowedPaymentsForPickup);

public record CheckoutCartResponse(
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("total_quantity")] int TotalQuantity,
    [property: JsonPropertyName("items")] List<CheckoutCartItemResponse> Items,
    [property: JsonPropertyName("has_unavailable_items")] bool HasUnavailableItems = false,
    [property: JsonPropertyName("unavailable_items_count")] int UnavailableItemsCount = 0,
    [property: JsonPropertyName("requires_unavailable_items_confirmation")] bool RequiresUnavailableItemsConfirmation = false,
    [property: JsonPropertyName("unavailable_items")] List<CheckoutUnavailableCartItemResponse>? UnavailableItems = null);

public record CheckoutCartItemResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("product_id")] Guid ProductId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("total_price")] decimal TotalPrice,
    [property: JsonPropertyName("variant_display_size")] string? VariantDisplaySize,
    [property: JsonPropertyName("package_type_name")] string? PackageTypeName,
    [property: JsonPropertyName("measurement_value")] decimal? MeasurementValue,
    [property: JsonPropertyName("measurement_unit_name")] string? MeasurementUnitName,
    [property: JsonPropertyName("variant_image_url")] string? VariantImageUrl,
    [property: JsonPropertyName("variant_images")] IReadOnlyList<string> VariantImages);

public record CheckoutUnavailableCartItemResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("product_id")] Guid ProductId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("availability_status")] string AvailabilityStatus);

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
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("is_available")] bool IsAvailable,
    [property: JsonPropertyName("is_default")] bool IsDefault);

public record CheckoutPromoCodeResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("discount_type")] string DiscountType,
    [property: JsonPropertyName("discount_value")] decimal DiscountValue,
    [property: JsonPropertyName("discount_amount")] decimal DiscountAmount);

public record CheckoutDeliveryCheckResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("is_deliverable")] bool IsDeliverable,
    [property: JsonPropertyName("can_proceed_to_checkout")] bool CanProceedToCheckout,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("message_ar")] string MessageAr,
    [property: JsonPropertyName("message_en")] string MessageEn,
    [property: JsonPropertyName("delivery_fee")] decimal? DeliveryFee,
    [property: JsonPropertyName("distance_km")] decimal? DistanceKm);

public record CheckoutEstimatedDeliveryWindowResponse(
    [property: JsonPropertyName("min_minutes")] int MinMinutes,
    [property: JsonPropertyName("max_minutes")] int MaxMinutes,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("subtitle")] string Subtitle,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("is_approximate")] bool IsApproximate,
    [property: JsonPropertyName("calculation_mode")] string? CalculationMode,
    [property: JsonPropertyName("explanation")] string? Explanation);

public record CheckoutDeliveryQuoteResponse(
    [property: JsonPropertyName("distance_km")] decimal DistanceKm,
    [property: JsonPropertyName("base_fee")] decimal BaseFee,
    [property: JsonPropertyName("distance_fee")] decimal DistanceFee,
    [property: JsonPropertyName("surge_fee")] decimal SurgeFee,
    [property: JsonPropertyName("total_fee")] decimal TotalFee,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("rule_label")] string RuleLabel);

public record CheckoutDeliveryLegResponse(
    [property: JsonPropertyName("distance_km")] decimal DistanceKm,
    [property: JsonPropertyName("fee")] decimal Fee,
    [property: JsonPropertyName("pricing_source")] string PricingSource);

public record CheckoutDeliveryBreakdownResponse(
    [property: JsonPropertyName("driver_to_vendor")] CheckoutDeliveryLegResponse DriverToVendor,
    [property: JsonPropertyName("vendor_to_customer")] CheckoutDeliveryLegResponse VendorToCustomer,
    [property: JsonPropertyName("total_delivery")] decimal TotalDelivery,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("used_estimated_driver_pricing")] bool UsedEstimatedDriverPricing);

public record CheckoutShippingBreakdownLineResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("amount")] decimal Amount);

public record CheckoutSummaryTotalsResponse(
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("shipping_cost")] decimal ShippingCost,
    [property: JsonPropertyName("discount")] decimal Discount,
    [property: JsonPropertyName("vat_amount")] decimal VatAmount,
    [property: JsonPropertyName("cod_fee")] decimal CodFee,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("currency")] string Currency);

public record ApplyCheckoutPromoCodeRequest(
    [property: JsonPropertyName("code")] string Code);

public record ApplyCheckoutPromoCodeResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("cart")] CheckoutCartResponse Cart,
    [property: JsonPropertyName("address_id")] Guid? AddressId,
    [property: JsonPropertyName("selected_address")] CheckoutSelectedAddressResponse? SelectedAddress,
    [property: JsonPropertyName("available_addresses")] List<CheckoutSelectedAddressResponse> AvailableAddresses,
    [property: JsonPropertyName("delivery_slots")] List<CheckoutDeliverySlotResponse> DeliverySlots,
    [property: JsonPropertyName("payment_methods")] List<CheckoutPaymentMethodResponse> PaymentMethods,
    [property: JsonPropertyName("promo_code")] CheckoutPromoCodeResponse? PromoCode,
    [property: JsonPropertyName("delivery_check")] CheckoutDeliveryCheckResponse DeliveryCheck,
    [property: JsonPropertyName("estimated_delivery_window")] CheckoutEstimatedDeliveryWindowResponse? EstimatedDeliveryWindow,
    [property: JsonPropertyName("delivery_quote")] CheckoutDeliveryQuoteResponse? DeliveryQuote,
    [property: JsonPropertyName("delivery_breakdown")] CheckoutDeliveryBreakdownResponse? DeliveryBreakdown,
    [property: JsonPropertyName("shipping_breakdown")] List<CheckoutShippingBreakdownLineResponse> ShippingBreakdown,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("summary")] CheckoutSummaryTotalsResponse Summary);

public record RemoveCheckoutPromoCodeResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("cart")] CheckoutCartResponse Cart,
    [property: JsonPropertyName("address_id")] Guid? AddressId,
    [property: JsonPropertyName("selected_address")] CheckoutSelectedAddressResponse? SelectedAddress,
    [property: JsonPropertyName("available_addresses")] List<CheckoutSelectedAddressResponse> AvailableAddresses,
    [property: JsonPropertyName("delivery_slots")] List<CheckoutDeliverySlotResponse> DeliverySlots,
    [property: JsonPropertyName("payment_methods")] List<CheckoutPaymentMethodResponse> PaymentMethods,
    [property: JsonPropertyName("promo_code")] CheckoutPromoCodeResponse? PromoCode,
    [property: JsonPropertyName("delivery_check")] CheckoutDeliveryCheckResponse DeliveryCheck,
    [property: JsonPropertyName("estimated_delivery_window")] CheckoutEstimatedDeliveryWindowResponse? EstimatedDeliveryWindow,
    [property: JsonPropertyName("delivery_quote")] CheckoutDeliveryQuoteResponse? DeliveryQuote,
    [property: JsonPropertyName("delivery_breakdown")] CheckoutDeliveryBreakdownResponse? DeliveryBreakdown,
    [property: JsonPropertyName("shipping_breakdown")] List<CheckoutShippingBreakdownLineResponse> ShippingBreakdown,
    [property: JsonPropertyName("pricing_mode")] string PricingMode,
    [property: JsonPropertyName("summary")] CheckoutSummaryTotalsResponse Summary);

public record GetCartDeliveryCheckResponse(
    [property: JsonPropertyName("address_id")] Guid? AddressId,
    [property: JsonPropertyName("selected_address")] CheckoutSelectedAddressResponse? SelectedAddress,
    [property: JsonPropertyName("delivery_check")] CheckoutDeliveryCheckResponse DeliveryCheck,
    [property: JsonPropertyName("delivery_quote")] CheckoutDeliveryQuoteResponse DeliveryQuote);

public class PlaceOrderRequest
{
    [JsonPropertyName("vendor_id")]
    public Guid? VendorId { get; init; }

    [JsonPropertyName("address_id")]
    public Guid? AddressId { get; init; }

    [JsonPropertyName("delivery_slot_id")]
    public string? DeliverySlotId { get; init; }

    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; init; }

    [JsonPropertyName("promo_code")]
    public string? PromoCode { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("remove_unavailable_items")]
    public bool RemoveUnavailableItems { get; init; }

    [JsonPropertyName("fulfillment_type")]
    public string? FulfillmentType { get; init; }

    [JsonPropertyName("vendor_branch_id")]
    public Guid? VendorBranchId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    [JsonIgnore]
    public Guid? EffectiveVendorId => VendorId ?? ReadGuid("vendorId");

    [JsonIgnore]
    public Guid? EffectiveAddressId => NormalizeGuid(AddressId) ?? ReadGuid("addressId");

    [JsonIgnore]
    public string? EffectiveDeliverySlotId => DeliverySlotId ?? ReadString("deliverySlotId");

    [JsonIgnore]
    public string EffectivePaymentMethod => NormalizePaymentMethod(PaymentMethod ?? ReadString("paymentMethod"));

    [JsonIgnore]
    public string? EffectivePromoCode => PromoCode ?? ReadString("promoCode");

    [JsonIgnore]
    public string? EffectiveNotes => Notes ?? ReadString("note") ?? ReadString("notes");

    [JsonIgnore]
    public bool EffectiveRemoveUnavailableItems =>
        RemoveUnavailableItems ||
        ReadBool("removeUnavailableItems") ||
        ReadBool("allowPartialCheckout") ||
        ReadBool("confirmUnavailableItemsRemoval");

    [JsonIgnore]
    public string EffectiveFulfillmentType =>
        NormalizeFulfillmentType(FulfillmentType ?? ReadString("fulfillmentType"));

    [JsonIgnore]
    public Guid? EffectiveVendorBranchId => NormalizeGuid(VendorBranchId) ?? ReadGuid("vendorBranchId");

    [JsonIgnore]
    public OrderFulfillmentType EffectiveFulfillment =>
        EffectiveFulfillmentType == "pickup"
            ? OrderFulfillmentType.Pickup
            : OrderFulfillmentType.Delivery;

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

    private static Guid? NormalizeGuid(Guid? value) =>
        value.HasValue && value.Value != Guid.Empty ? value : null;

    private string? ReadString(string propertyName)
    {
        if (ExtensionData is null || !ExtensionData.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
    }

    private bool ReadBool(string propertyName)
    {
        if (ExtensionData is null || !ExtensionData.TryGetValue(propertyName, out var value))
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return value.ValueKind == JsonValueKind.String &&
               bool.TryParse(value.GetString(), out var parsed) &&
               parsed;
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

    private static string NormalizeFulfillmentType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pickup" => "pickup",
            _ => "delivery"
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
    [property: JsonPropertyName("provider_reference")] string ProviderReference,
    [property: JsonPropertyName("provider_config")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? ProviderConfig = null,
    [property: JsonPropertyName("payment_flow")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PaymentFlow = null,
    [property: JsonPropertyName("is_paid")] bool IsPaid = false,
    [property: JsonPropertyName("requires_customer_action")] bool RequiresCustomerAction = true,
    [property: JsonPropertyName("customer_action")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CustomerAction = null,
    [property: JsonPropertyName("confirmation_mode")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ConfirmationMode = null);
