using System.Globalization;

namespace Zadana.Application.Modules.Orders.DTOs;

public sealed record CartVendorPriceDto
{
    public CartVendorPriceDto(Guid id, string name, decimal price, decimal? oldPrice, bool isDiscounted)
    {
        Id = id;
        Name = name;
        Price = price;
        OldPrice = oldPrice;
        IsDiscounted = isDiscounted;
    }

    public CartVendorPriceDto(Guid id, string name, decimal price, decimal? oldPrice, bool isDiscounted, string? _, string? __)
        : this(id, name, price, oldPrice, isDiscounted)
    {
    }

    public Guid Id { get; init; }
    public string Name { get; init; }
    public decimal Price { get; init; }
    public decimal? OldPrice { get; init; }
    public bool IsDiscounted { get; init; }
}

public sealed record CartAvailableVendorDto
{
    public CartAvailableVendorDto(
        Guid id,
        string name,
        string? logoUrl,
        int productsCount,
        bool isOnlineNow = true,
        string? unavailableReason = null)
    {
        Id = id;
        Name = name;
        LogoUrl = logoUrl;
        ProductsCount = productsCount;
        IsOnlineNow = isOnlineNow;
        UnavailableReason = unavailableReason;
    }

    public CartAvailableVendorDto(Guid id, string name, string? logoUrl, int productsCount, string? _, string? __)
        : this(id, name, logoUrl, productsCount)
    {
    }

    public Guid Id { get; init; }
    public string Name { get; init; }
    public string? LogoUrl { get; init; }
    public int ProductsCount { get; init; }
    public bool IsOnlineNow { get; init; }
    public string? UnavailableReason { get; init; }
}

public record CartAvailableVendorsDto(
    List<CartAvailableVendorDto> Vendors,
    int Total = 0,
    int Limit = 0,
    int Offset = 0,
    bool HasMore = false);

public sealed record CartItemDto
{
    public CartItemDto(
        Guid id,
        Guid productId,
        string name,
        string? imageUrl,
        string? unit,
        int quantity,
        List<CartVendorPriceDto> vendorPrices,
        bool isAvailable = true,
        string? availabilityStatus = null)
    {
        Id = id;
        ProductId = productId;
        Name = name;
        ImageUrl = imageUrl;
        Unit = unit;
        Quantity = quantity;
        VendorPrices = vendorPrices;
        IsAvailable = isAvailable;
        AvailabilityStatus = availabilityStatus;
    }

    public CartItemDto(
        Guid id,
        Guid productId,
        string name,
        string? imageUrl,
        string? unit,
        int quantity,
        List<CartVendorPriceDto> vendorPrices,
        bool isAvailable,
        string? availabilityStatus,
        string? variantDisplaySize,
        string? packageTypeName,
        decimal? measurementValue,
        string? measurementUnitName,
        string? variantImageUrl,
        IReadOnlyList<string>? variantImages)
        : this(id, productId, name, imageUrl, unit, quantity, vendorPrices, isAvailable, availabilityStatus)
    {
        VariantDisplaySize = variantDisplaySize;
        PackageTypeName = packageTypeName;
        MeasurementValue = measurementValue;
        MeasurementUnitName = measurementUnitName;
        VariantImageUrl = variantImageUrl;
        VariantImages = variantImages?.ToList() ?? [];
    }

    public CartItemDto(
        Guid id,
        Guid productId,
        string name,
        string? imageUrl,
        string? unit,
        int quantity,
        List<CartVendorPriceDto> vendorPrices,
        bool isAvailable,
        string? availabilityStatus,
        string? _,
        string? __,
        string? ___,
        string? ____)
        : this(id, productId, name, imageUrl, unit, quantity, vendorPrices, isAvailable, availabilityStatus)
    {
    }

    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string Name { get; init; }
    public string? ImageUrl { get; init; }
    public string? Unit { get; init; }
    public int Quantity { get; init; }
    public List<CartVendorPriceDto> VendorPrices { get; init; }
    public bool IsAvailable { get; init; }
    public string? AvailabilityStatus { get; init; }
    public string? VariantDisplaySize { get; init; }
    public string? PackageTypeName { get; init; }
    public decimal? MeasurementValue { get; init; }
    public string? MeasurementUnitName { get; init; }
    public string? VariantImageUrl { get; init; }
    public IReadOnlyList<string> VariantImages { get; init; } = [];
}

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
    CartSummaryDto Summary,
    int Total = 0,
    int Limit = 0,
    int Offset = 0,
    bool HasMore = false);

public sealed record CartItemMutationResponseDto
{
    public CartItemMutationResponseDto(string message, CartItemDto item, CartSummaryDto summary)
    {
        Message = message;
        Item = item;
        Summary = summary;
    }

    public CartItemMutationResponseDto(string messageAr, string messageEn, CartItemDto item, CartSummaryDto summary)
        : this(Localize(messageAr, messageEn), item, summary)
    {
    }

    public string Message { get; init; }
    public CartItemDto Item { get; init; }
    public CartSummaryDto Summary { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

public sealed record CartItemRemovalResponseDto
{
    public CartItemRemovalResponseDto(string message, CartSummaryDto summary)
    {
        Message = message;
        Summary = summary;
    }

    public CartItemRemovalResponseDto(string messageAr, string messageEn, CartSummaryDto summary)
        : this(Localize(messageAr, messageEn), summary)
    {
    }

    public string Message { get; init; }
    public CartSummaryDto Summary { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

public sealed record CartClearResponseDto
{
    public CartClearResponseDto(string message)
    {
        Message = message;
    }

    public CartClearResponseDto(string messageAr, string messageEn)
        : this(Localize(messageAr, messageEn))
    {
    }

    public string Message { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}
