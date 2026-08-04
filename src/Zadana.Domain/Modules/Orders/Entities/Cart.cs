using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class Cart : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string? GuestId { get; private set; }
    public Guid? CouponId { get; private set; }
    public Guid? SelectedVendorId { get; private set; }
    
    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public decimal BaseDeliveryFee { get; private set; }
    public decimal DistanceDeliveryFee { get; private set; }
    public decimal SurgeDeliveryFee { get; private set; }
    public decimal? QuotedDistanceKm { get; private set; }
    public string? DeliveryPricingMode { get; private set; }
    public string? DeliveryPricingRuleLabel { get; private set; }
    public decimal DriverToVendorDistanceKm { get; private set; }
    public decimal VendorToCustomerDistanceKm { get; private set; }
    public decimal DriverToVendorFee { get; private set; }
    public decimal VendorToCustomerFee { get; private set; }
    public string? DriverToVendorPricingSource { get; private set; }
    public string? VendorToCustomerPricingSource { get; private set; }
    public bool UsedEstimatedDriverPricing { get; private set; }
    public string? PricingOriginType { get; private set; }
    public Guid? PricingOriginDriverId { get; private set; }
    public string? DeliveryQuoteStatus { get; private set; }
    public DateTime? DeliveryQuoteLockedAtUtc { get; private set; }
    public int DeliveryQuoteVersion { get; private set; }
    public bool HasDeliveryAnomalyWarning { get; private set; }
    public decimal Total { get; private set; }

    // Navigation
    public User? User { get; private set; }
    // Note: Coupon is resolved from Marketing module
    
    public ICollection<CartItem> Items { get; private set; } = [];

    private Cart() { }

    public Cart(Guid? userId, string? guestId = null)
    {
        if (!userId.HasValue && string.IsNullOrWhiteSpace(guestId))
        {
            throw new InvalidOperationException("Cart owner is required.");
        }

        UserId = userId;
        GuestId = guestId?.Trim();
        Subtotal = 0;
        DiscountTotal = 0;
        DeliveryFee = 0;
        BaseDeliveryFee = 0;
        DistanceDeliveryFee = 0;
        SurgeDeliveryFee = 0;
        DriverToVendorDistanceKm = 0;
        VendorToCustomerDistanceKm = 0;
        DriverToVendorFee = 0;
        VendorToCustomerFee = 0;
        DeliveryQuoteVersion = 1;
        Total = 0;
    }

    public void ApplyCoupon(Guid couponId, decimal discountAmount)
    {
        CouponId = couponId;
        DiscountTotal = discountAmount;
        RecalculateTotal();
    }

    public void RemoveCoupon()
    {
        CouponId = null;
        DiscountTotal = 0;
        RecalculateTotal();
    }

    public void SelectVendor(Guid? vendorId)
    {
        SelectedVendorId = vendorId.HasValue && vendorId.Value != Guid.Empty
            ? vendorId.Value
            : null;
    }

    public void UpdateTotals(
        decimal subtotal,
        decimal deliveryFee,
        decimal baseDeliveryFee = 0,
        decimal distanceDeliveryFee = 0,
        decimal surgeDeliveryFee = 0,
        decimal? quotedDistanceKm = null,
        string? deliveryPricingMode = null,
        string? deliveryPricingRuleLabel = null,
        decimal driverToVendorDistanceKm = 0,
        decimal vendorToCustomerDistanceKm = 0,
        decimal driverToVendorFee = 0,
        decimal vendorToCustomerFee = 0,
        string? driverToVendorPricingSource = null,
        string? vendorToCustomerPricingSource = null,
        bool usedEstimatedDriverPricing = false,
        string? pricingOriginType = null,
        Guid? pricingOriginDriverId = null,
        string? deliveryQuoteStatus = null,
        DateTime? deliveryQuoteLockedAtUtc = null,
        int deliveryQuoteVersion = 1,
        bool hasDeliveryAnomalyWarning = false)
    {
        Subtotal = subtotal;
        DeliveryFee = deliveryFee;
        BaseDeliveryFee = baseDeliveryFee;
        DistanceDeliveryFee = distanceDeliveryFee;
        SurgeDeliveryFee = surgeDeliveryFee;
        QuotedDistanceKm = quotedDistanceKm;
        DeliveryPricingMode = string.IsNullOrWhiteSpace(deliveryPricingMode) ? null : deliveryPricingMode.Trim();
        DeliveryPricingRuleLabel = string.IsNullOrWhiteSpace(deliveryPricingRuleLabel) ? null : deliveryPricingRuleLabel.Trim();
        DriverToVendorDistanceKm = driverToVendorDistanceKm;
        VendorToCustomerDistanceKm = vendorToCustomerDistanceKm;
        DriverToVendorFee = driverToVendorFee;
        VendorToCustomerFee = vendorToCustomerFee;
        DriverToVendorPricingSource = string.IsNullOrWhiteSpace(driverToVendorPricingSource) ? null : driverToVendorPricingSource.Trim();
        VendorToCustomerPricingSource = string.IsNullOrWhiteSpace(vendorToCustomerPricingSource) ? null : vendorToCustomerPricingSource.Trim();
        UsedEstimatedDriverPricing = usedEstimatedDriverPricing;
        PricingOriginType = string.IsNullOrWhiteSpace(pricingOriginType) ? null : pricingOriginType.Trim();
        PricingOriginDriverId = pricingOriginDriverId;
        DeliveryQuoteStatus = string.IsNullOrWhiteSpace(deliveryQuoteStatus) ? null : deliveryQuoteStatus.Trim();
        DeliveryQuoteLockedAtUtc = deliveryQuoteLockedAtUtc;
        DeliveryQuoteVersion = deliveryQuoteVersion <= 0 ? 1 : deliveryQuoteVersion;
        HasDeliveryAnomalyWarning = hasDeliveryAnomalyWarning;
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        Total = Math.Max(0, Subtotal - DiscountTotal + DeliveryFee);
    }
}
