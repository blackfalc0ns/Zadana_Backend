using System.Globalization;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Modules.Checkout.DTOs;

public record CheckoutSummaryDto(
    CheckoutCartDto Cart,
    CheckoutSelectedAddressDto? SelectedAddress,
    List<CheckoutSelectedAddressDto> AvailableAddresses,
    List<CheckoutDeliverySlotDto> DeliverySlots,
    List<CheckoutPaymentMethodDto> PaymentMethods,
    CheckoutPromoCodeDto? PromoCode,
    CheckoutDeliveryCheckDto DeliveryCheck,
    CheckoutEstimatedDeliveryWindowDto EstimatedDeliveryWindow,
    CheckoutDeliveryQuoteDto DeliveryQuote,
    CheckoutDeliveryBreakdownDto DeliveryBreakdown,
    List<CheckoutShippingBreakdownLineDto> ShippingBreakdown,
    string PricingMode,
    CheckoutTotalsDto Summary);

public record CheckoutCartDto(
    int ItemsCount,
    int TotalQuantity,
    List<CheckoutCartItemDto> Items);

public sealed record CheckoutCartItemDto
{
    public CheckoutCartItemDto(
        Guid id,
        Guid productId,
        string name,
        string? imageUrl,
        string? unit,
        int quantity,
        decimal price,
        decimal totalPrice)
    {
        Id = id;
        ProductId = productId;
        Name = name;
        ImageUrl = imageUrl;
        Unit = unit;
        Quantity = quantity;
        Price = price;
        TotalPrice = totalPrice;
    }

    public CheckoutCartItemDto(
        Guid id,
        Guid productId,
        string name,
        string? imageUrl,
        string? unit,
        int quantity,
        decimal price,
        decimal totalPrice,
        string? variantDisplaySize,
        string? packageTypeName,
        decimal? measurementValue,
        string? measurementUnitName,
        string? variantImageUrl,
        IReadOnlyList<string>? variantImages)
        : this(id, productId, name, imageUrl, unit, quantity, price, totalPrice)
    {
        VariantDisplaySize = variantDisplaySize;
        PackageTypeName = packageTypeName;
        MeasurementValue = measurementValue;
        MeasurementUnitName = measurementUnitName;
        VariantImageUrl = variantImageUrl;
        VariantImages = variantImages?.ToList() ?? [];
    }

    public CheckoutCartItemDto(
        Guid id,
        Guid productId,
        string name,
        string? imageUrl,
        string? unit,
        int quantity,
        decimal price,
        decimal totalPrice,
        string? _,
        string? __,
        string? ___,
        string? ____)
        : this(id, productId, name, imageUrl, unit, quantity, price, totalPrice)
    {
    }

    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string Name { get; init; }
    public string? ImageUrl { get; init; }
    public string? Unit { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal TotalPrice { get; init; }
    public string? VariantDisplaySize { get; init; }
    public string? PackageTypeName { get; init; }
    public decimal? MeasurementValue { get; init; }
    public string? MeasurementUnitName { get; init; }
    public string? VariantImageUrl { get; init; }
    public IReadOnlyList<string> VariantImages { get; init; } = [];
}

public record CheckoutSelectedAddressDto(
    Guid Id,
    string Label,
    string AddressLine,
    bool IsDefault);

public sealed record CheckoutDeliverySlotDto
{
    public CheckoutDeliverySlotDto(string id, string label, DateTime startAt, DateTime endAt, bool isAvailable, bool isSelected)
    {
        Id = id;
        Label = label;
        StartAt = startAt;
        EndAt = endAt;
        IsAvailable = isAvailable;
        IsSelected = isSelected;
    }

    public CheckoutDeliverySlotDto(string id, string labelAr, string labelEn, DateTime startAt, DateTime endAt, bool isAvailable, bool isSelected)
        : this(id, Localize(labelAr, labelEn), startAt, endAt, isAvailable, isSelected)
    {
    }

    public string Id { get; init; }
    public string Label { get; init; }
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsSelected { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

public sealed record CheckoutPaymentMethodDto
{
    public CheckoutPaymentMethodDto(string code, string label, string? description, bool isAvailable, bool isDefault)
    {
        Code = code;
        Label = label;
        Description = description;
        IsAvailable = isAvailable;
        IsDefault = isDefault;
    }

    public CheckoutPaymentMethodDto(string code, string labelAr, string labelEn, string? descriptionAr, string? descriptionEn, bool isAvailable, bool isDefault)
        : this(code, Localize(labelAr, labelEn), LocalizeNullable(descriptionAr, descriptionEn), isAvailable, isDefault)
    {
    }

    public string Code { get; init; }
    public string Label { get; init; }
    public string? Description { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsDefault { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;

    private static string? LocalizeNullable(string? arabic, string? english)
    {
        var value = Localize(arabic ?? string.Empty, english ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public record CheckoutPromoCodeDto(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount);

public record CheckoutDeliveryCheckDto(
    string Status,
    bool IsDeliverable,
    bool CanProceedToCheckout,
    string MessageAr,
    string MessageEn,
    decimal? DeliveryFee,
    decimal? DistanceKm);

public sealed record CheckoutEstimatedDeliveryWindowDto
{
    public CheckoutEstimatedDeliveryWindowDto(int minMinutes, int maxMinutes, string confidence, string source, bool isApproximate, string? calculationMode = null, string? explanation = null)
    {
        MinMinutes = minMinutes;
        MaxMinutes = maxMinutes;
        Confidence = confidence;
        Source = source;
        IsApproximate = isApproximate;
        CalculationMode = calculationMode;
        Explanation = explanation;
        Title = DeliveryEtaWindowDisplayTextBuilder.BuildTitle();
        Label = DeliveryEtaWindowLabelBuilder.Build(minMinutes, maxMinutes, isApproximate);
        Subtitle = DeliveryEtaWindowDisplayTextBuilder.BuildSubtitle(confidence, isApproximate);
    }

    public int MinMinutes { get; init; }
    public int MaxMinutes { get; init; }
    public string Title { get; init; }
    public string Label { get; init; }
    public string Subtitle { get; init; }
    public string Confidence { get; init; }
    public string Source { get; init; }
    public bool IsApproximate { get; init; }
    public string? CalculationMode { get; init; }
    public string? Explanation { get; init; }
}

public record CheckoutTotalsDto(
    decimal Subtotal,
    decimal ShippingCost,
    decimal Discount,
    decimal VatAmount,
    decimal CodFee,
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

public record CheckoutDeliveryLegDto(
    decimal DistanceKm,
    decimal Fee,
    string PricingSource);

public record CheckoutDeliveryBreakdownDto(
    CheckoutDeliveryLegDto DriverToVendor,
    CheckoutDeliveryLegDto VendorToCustomer,
    decimal TotalDelivery,
    string PricingMode,
    bool UsedEstimatedDriverPricing);

public sealed record CheckoutShippingBreakdownLineDto
{
    public CheckoutShippingBreakdownLineDto(string code, string label, decimal amount)
    {
        Code = code;
        Label = label;
        Amount = amount;
    }

    public CheckoutShippingBreakdownLineDto(string code, string labelAr, string labelEn, decimal amount)
        : this(code, Localize(labelAr, labelEn), amount)
    {
    }

    public string Code { get; init; }
    public string Label { get; init; }
    public decimal Amount { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

public sealed record ApplyCheckoutPromoCodeResultDto
{
    public ApplyCheckoutPromoCodeResultDto(string message, CheckoutPromoCodeDto promoCode, CheckoutTotalsDto summary)
    {
        Message = message;
        PromoCode = promoCode;
        Summary = summary;
    }

    public ApplyCheckoutPromoCodeResultDto(string messageAr, string messageEn, CheckoutPromoCodeDto promoCode, CheckoutTotalsDto summary)
        : this(Localize(messageAr, messageEn), promoCode, summary)
    {
    }

    public string Message { get; init; }
    public CheckoutPromoCodeDto PromoCode { get; init; }
    public CheckoutTotalsDto Summary { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

public sealed record RemoveCheckoutPromoCodeResultDto
{
    public RemoveCheckoutPromoCodeResultDto(string message, CheckoutTotalsDto summary)
    {
        Message = message;
        Summary = summary;
    }

    public RemoveCheckoutPromoCodeResultDto(string messageAr, string messageEn, CheckoutTotalsDto summary)
        : this(Localize(messageAr, messageEn), summary)
    {
    }

    public string Message { get; init; }
    public CheckoutTotalsDto Summary { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

public sealed record PlaceCheckoutOrderResultDto
{
    public PlaceCheckoutOrderResultDto(string message, CheckoutPlacedOrderDto order, CheckoutPaymentSessionDto? payment)
    {
        Message = message;
        Order = order;
        Payment = payment;
    }

    public PlaceCheckoutOrderResultDto(string messageAr, string messageEn, CheckoutPlacedOrderDto order, CheckoutPaymentSessionDto? payment)
        : this(Localize(messageAr, messageEn), order, payment)
    {
    }

    public string Message { get; init; }
    public CheckoutPlacedOrderDto Order { get; init; }
    public CheckoutPaymentSessionDto? Payment { get; init; }

    private static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}

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
    string ProviderReference,
    object? ProviderConfig = null,
    string? PaymentFlow = null,
    bool IsPaid = false,
    bool RequiresCustomerAction = true,
    string? CustomerAction = null,
    string? ConfirmationMode = null);
