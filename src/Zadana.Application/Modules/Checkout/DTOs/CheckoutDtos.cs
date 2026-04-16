namespace Zadana.Application.Modules.Checkout.DTOs;

public record CheckoutSummaryDto(
    CheckoutCartDto Cart,
    CheckoutSelectedAddressDto? SelectedAddress,
    List<CheckoutDeliverySlotDto> DeliverySlots,
    List<CheckoutPaymentMethodDto> PaymentMethods,
    CheckoutPromoCodeDto? PromoCode,
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
    string Label,
    DateTime StartAt,
    DateTime EndAt,
    bool IsAvailable,
    bool IsSelected);

public record CheckoutPaymentMethodDto(
    string Code,
    string Label,
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

public record ApplyCheckoutPromoCodeResultDto(
    string Message,
    CheckoutPromoCodeDto PromoCode,
    CheckoutTotalsDto Summary);

public record RemoveCheckoutPromoCodeResultDto(
    string Message,
    CheckoutTotalsDto Summary);

public record PlaceCheckoutOrderResultDto(
    string Message,
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
