namespace Zadana.Application.Modules.Finances.DTOs;

public record AdminOrderFinancialBreakdownDto(
    Guid OrderId,
    string OrderRef,
    decimal Subtotal,
    decimal Discounts,
    decimal CouponDiscount,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal CodFee,
    decimal Vat,
    decimal Total,
    decimal VendorEarnings,
    decimal VendorCommission,
    decimal DriverPayout,
    decimal PlatformRevenue,
    decimal NetMargin,
    decimal MarginPercent,
    string? FulfillmentType = null);
