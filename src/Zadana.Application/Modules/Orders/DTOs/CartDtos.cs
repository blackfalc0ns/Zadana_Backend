namespace Zadana.Application.Modules.Orders.DTOs;

public record CartVendorPriceDto(
    Guid Id,
    string Name,
    decimal Price,
    decimal? OldPrice,
    bool IsDiscounted);

public record CartAvailableVendorDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    int ProductsCount);

public record CartAvailableVendorsDto(
    List<CartAvailableVendorDto> Vendors);

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    string? ImageUrl,
    string? Unit,
    int Quantity,
    List<CartVendorPriceDto> VendorPrices,
    bool IsAvailable = true,
    string? AvailabilityStatus = null);

public record CartSummaryDto(
    int ItemsCount,
    int TotalQuantity,
    decimal? Subtotal,
    decimal? DiscountAmount,
    decimal? TotalAmount,
    bool IsPricingAvailable = false,
    bool CanCheckout = false,
    string? CheckoutBlockReason = null,
    bool HasUnavailableItems = false,
    int UnavailableItemsCount = 0);

public record CartDto(
    List<CartItemDto> Items,
    CartSummaryDto Summary);

public record CartItemMutationResponseDto(
    string MessageAr,
    string MessageEn,
    CartItemDto Item,
    CartSummaryDto Summary);

public record CartItemRemovalResponseDto(
    string MessageAr,
    string MessageEn,
    CartSummaryDto Summary);

public record CartClearResponseDto(
    string MessageAr,
    string MessageEn);
