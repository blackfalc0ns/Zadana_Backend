using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid? VendorBranchId { get; private set; }
    public Guid CustomerAddressId { get; private set; } // Reference to Delivery module
    public Guid? CouponId { get; private set; } // Reference to Marketing module
    
    public OrderStatus Status { get; private set; }
    public PaymentMethodType PaymentMethod { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    
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
    public decimal? ActualAssignedDriverPickupDistanceKm { get; private set; }
    public decimal? ActualDispatchDeviationPercent { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal CodFee { get; private set; }
    public decimal TotalAmount { get; private set; }
    
    /// <summary>
    /// Gross product amount before any discount. Mirrors <see cref="Subtotal"/>
    /// for orders created before the SAR-only revision; populated explicitly
    /// for new orders. Used by the revenue-distribution formula
    /// (<c>VendorNet = ProductNet - VendorCommissionAmount - VendorRecoveryApplied</c>).
    /// </summary>
    public decimal ProductGross { get; private set; }

    /// <summary>
    /// Net product amount after discount: <c>ProductGross - DiscountTotal</c>.
    /// </summary>
    public decimal ProductNet { get; private set; }

    /// <summary>
    /// Order currency. SAR for new orders; legacy data may carry a different value.
    /// </summary>
    public string Currency { get; private set; } = "SAR";

    /// <summary>Vendor commission charged on this order. Pre-revision orders mirror <see cref="CommissionAmount"/>.</summary>
    public decimal VendorCommissionAmount { get; private set; }

    /// <summary>Driver commission charged on the delivery fee. 0 for legacy orders until backfilled.</summary>
    public decimal DriverCommissionAmount { get; private set; }

    /// <summary>How the price was determined: <c>live</c>, <c>quoted</c>, or <c>manual</c>.</summary>
    public string PricingMode { get; private set; } = "live";

    /// <summary>Snapshot of the tax policy at order creation (JSON). Null for legacy orders.</summary>
    public string? TaxPolicySnapshot { get; private set; }

    /// <summary>Snapshot of the commission policy at order creation (JSON). Null for legacy orders.</summary>
    public string? CommissionPolicySnapshot { get; private set; }
    
    public string? Notes { get; private set; }
    
    public DateTime PlacedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public int? EtaMinMinutes { get; private set; }
    public int? EtaMaxMinutes { get; private set; }
    public string? EtaConfidence { get; private set; }
    public string? EtaSource { get; private set; }
    public bool? EtaIsApproximate { get; private set; }
    public string? EtaCalculationMode { get; private set; }
    public string? EtaExplanation { get; private set; }
    public DateTime? EtaCalculatedAtUtc { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;
    public VendorBranch? VendorBranch { get; private set; }
    
    public ICollection<OrderItem> Items { get; private set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; private set; } = [];
    public ICollection<OrderComplaint> Complaints { get; private set; } = [];
    public ICollection<OrderSupportCase> SupportCases { get; private set; } = [];

    private Order() { }

    public Order(
        string orderNumber, 
        Guid userId, 
        Guid vendorId, 
        Guid customerAddressId,
        PaymentMethodType paymentMethod,
        decimal subtotal,
        decimal discountTotal,
        decimal deliveryFee,
        decimal baseDeliveryFee,
        decimal distanceDeliveryFee,
        decimal surgeDeliveryFee,
        decimal? quotedDistanceKm,
        string? deliveryPricingMode,
        string? deliveryPricingRuleLabel,
        decimal driverToVendorDistanceKm,
        decimal vendorToCustomerDistanceKm,
        decimal driverToVendorFee,
        decimal vendorToCustomerFee,
        string? driverToVendorPricingSource,
        string? vendorToCustomerPricingSource,
        bool usedEstimatedDriverPricing,
        string? pricingOriginType,
        Guid? pricingOriginDriverId,
        string? deliveryQuoteStatus,
        DateTime? deliveryQuoteLockedAtUtc,
        int deliveryQuoteVersion,
        bool hasDeliveryAnomalyWarning,
        decimal commissionAmount,
        decimal vatAmount = 0,
        decimal codFee = 0,
        string? notes = null,
        Guid? vendorBranchId = null,
        Guid? couponId = null)
    {
        OrderNumber = orderNumber;
        UserId = userId;
        VendorId = vendorId;
        CustomerAddressId = customerAddressId;
        PaymentMethod = paymentMethod;
        Subtotal = subtotal;
        DiscountTotal = discountTotal;
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
        CommissionAmount = commissionAmount;
        VatAmount = vatAmount;
        CodFee = codFee;
        TotalAmount = Math.Max(0, subtotal - discountTotal + deliveryFee + vatAmount + codFee);
        Notes = notes?.Trim();
        VendorBranchId = vendorBranchId;
        CouponId = couponId;

        // Revised SAR-only financial snapshot. Until call sites are upgraded
        // to pass these explicitly, derive sensible defaults from the legacy
        // inputs so the new ledger formula has consistent values to read.
        ProductGross = subtotal;
        ProductNet = Math.Max(0, subtotal - discountTotal);
        VendorCommissionAmount = commissionAmount;
        DriverCommissionAmount = 0m;
        Currency = "SAR";
        PricingMode = "live";
        TaxPolicySnapshot = null;
        CommissionPolicySnapshot = null;
        
        Status = OrderStatus.PendingPayment;
        PaymentStatus = Zadana.Domain.Modules.Payments.Enums.PaymentStatus.Initiated;
        PlacedAtUtc = DateTime.UtcNow;
    }

    public void ApplyFinancialSnapshot(
        decimal productGross,
        decimal productNet,
        decimal vendorCommissionAmount,
        decimal driverCommissionAmount,
        string currency,
        string pricingMode,
        string? taxPolicySnapshot,
        string? commissionPolicySnapshot)
    {
        if (productGross < 0 || productNet < 0 || vendorCommissionAmount < 0 || driverCommissionAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productGross), "Financial snapshot amounts cannot be negative.");
        }

        ProductGross = productGross;
        ProductNet = productNet;
        VendorCommissionAmount = vendorCommissionAmount;
        DriverCommissionAmount = driverCommissionAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "SAR" : currency.Trim().ToUpperInvariant();
        PricingMode = string.IsNullOrWhiteSpace(pricingMode) ? "live" : pricingMode.Trim();
        TaxPolicySnapshot = string.IsNullOrWhiteSpace(taxPolicySnapshot) ? null : taxPolicySnapshot;
        CommissionPolicySnapshot = string.IsNullOrWhiteSpace(commissionPolicySnapshot) ? null : commissionPolicySnapshot;
    }

    public void RecordAssignedDriverDistance(decimal pickupDistanceKm)
    {
        ActualAssignedDriverPickupDistanceKm = pickupDistanceKm;
        if (DriverToVendorDistanceKm <= 0)
        {
            ActualDispatchDeviationPercent = null;
            return;
        }

        ActualDispatchDeviationPercent = Math.Round(
            Math.Abs(pickupDistanceKm - DriverToVendorDistanceKm) / DriverToVendorDistanceKm * 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    public void ChangeStatus(OrderStatus newStatus, Guid? changedByUserId = null, string? note = null)
    {
        var oldStatus = Status;
        Status = newStatus;
        
        if (newStatus == OrderStatus.Delivered) DeliveredAtUtc = DateTime.UtcNow;
        if (newStatus == OrderStatus.Cancelled) CancelledAtUtc = DateTime.UtcNow;

        StatusHistory.Add(new OrderStatusHistory(Id, newStatus, changedByUserId, note, oldStatus));
    }

    public void UpdatePaymentStatus(PaymentStatus newStatus)
    {
        PaymentStatus = newStatus;
        if (newStatus == Zadana.Domain.Modules.Payments.Enums.PaymentStatus.Paid && Status == OrderStatus.PendingPayment)
        {
            ChangeStatus(
                PaymentMethod == PaymentMethodType.Card ? OrderStatus.PendingVendorAcceptance : OrderStatus.Placed,
                null,
                PaymentMethod == PaymentMethodType.Card ? "Online payment confirmed and awaiting vendor response" : "Payment confirmed");
        }
    }

    public void CaptureEtaSnapshot(
        int minMinutes,
        int maxMinutes,
        string confidence,
        string source,
        bool isApproximate,
        string? calculationMode,
        string? explanation,
        DateTime? calculatedAtUtc = null)
    {
        EtaMinMinutes = minMinutes > 0 ? minMinutes : null;
        EtaMaxMinutes = maxMinutes > 0 ? maxMinutes : null;
        EtaConfidence = string.IsNullOrWhiteSpace(confidence) ? null : confidence.Trim();
        EtaSource = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        EtaIsApproximate = isApproximate;
        EtaCalculationMode = string.IsNullOrWhiteSpace(calculationMode) ? null : calculationMode.Trim();
        EtaExplanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        EtaCalculatedAtUtc = calculatedAtUtc ?? DateTime.UtcNow;
    }
}
