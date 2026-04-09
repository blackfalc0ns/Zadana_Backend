namespace Zadana.Application.Modules.Orders.DTOs;

public record CartVendorPriceDto(
    Guid Id,
    string Name,
    decimal Price,
    decimal? OldPrice,
    bool IsDiscounted);

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    string? ImageUrl,
    string? Unit,
    int Quantity,
    List<CartVendorPriceDto> VendorPrices);

public record CartSummaryDto(
    int ItemsCount,
    int TotalQuantity);

public record CartDto(
    List<CartItemDto> Items,
    CartSummaryDto Summary);

public record CartItemMutationResponseDto(
    string Message,
    CartItemDto Item,
    CartSummaryDto Summary);

public record CartItemRemovalResponseDto(
    string Message,
    CartSummaryDto Summary);

public record CartClearResponseDto(
    string Message);
