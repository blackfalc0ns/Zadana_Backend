namespace Zadana.Application.Modules.Checkout.DTOs;

public record CheckoutSummaryDto(
    CheckoutCartDto Cart,
    CheckoutSelectedAddressDto? SelectedAddress,
    List<CheckoutSelectedAddressDto> AvailableAddresses,
    List<CheckoutDeliverySlotDto> DeliverySlots,
    List<CheckoutPaymentMethodDto> PaymentMethods,
    CheckoutPromoCodeDto? PromoCode,
    CheckoutDeliveryQuoteDto DeliveryQuote,
    List<CheckoutShippingBreakdownLineDto> ShippingBreakdown,
    string PricingMode,
    CheckoutTotalsDto Summary);

public record CheckoutCartDto(
    int ItemsCount,
    int TotalQuantity,
    List<CheckoutCartItemDto> Items);

public record CheckoutCartItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    string? ImageUrl,
    string? Unit,
    int Quantity,
    decimal Price,
    decimal TotalPrice);

public record CheckoutSelectedAddressDto(
    Guid Id,
    string Label,
    string AddressLine,
    bool IsDefault);

public record CheckoutDeliverySlotDto(
    string Id,
    string LabelAr,
    string LabelEn,
    DateTime StartAt,
    DateTime EndAt,
    bool IsAvailable,
    bool IsSelected);

public record CheckoutPaymentMethodDto(
    string Code,
    string LabelAr,
    string LabelEn,
    string? DescriptionAr,
    string? DescriptionEn,
    bool IsAvailable,
    bool IsDefault);

public record CheckoutPromoCodeDto(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount);

public record CheckoutTotalsDto(
    decimal Subtotal,
    decimal ShippingCost,
    decimal Discount,
    decimal Total,
    string Currency);

public record CheckoutDeliveryQuoteDto(
    decimal DistanceKm,
    decimal BaseFee,
    decimal DistanceFee,
    decimal SurgeFee,
    decimal TotalFee,
    string PricingMode,
    string RuleLabel);

public record CheckoutShippingBreakdownLineDto(
    string Code,
    string LabelAr,
    string LabelEn,
    decimal Amount);

public record ApplyCheckoutPromoCodeResultDto(
    string MessageAr,
    string MessageEn,
    CheckoutPromoCodeDto PromoCode,
    CheckoutTotalsDto Summary);

public record RemoveCheckoutPromoCodeResultDto(
    string MessageAr,
    string MessageEn,
    CheckoutTotalsDto Summary);

public record PlaceCheckoutOrderResultDto(
    string MessageAr,
    string MessageEn,
    CheckoutPlacedOrderDto Order,
    CheckoutPaymentSessionDto? Payment);

public record CheckoutPlacedOrderDto(
    Guid Id,
    DateTime CreatedAt,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    decimal TotalPrice);

public record CheckoutPaymentSessionDto(
    Guid Id,
    string Provider,
    string Status,
    string IframeUrl,
    string ProviderReference);
