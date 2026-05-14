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
    public decimal CommissionAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal CodFee { get; private set; }
    public decimal TotalAmount { get; private set; }
    
    public string? Notes { get; private set; }
    
    public DateTime PlacedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

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
        CommissionAmount = commissionAmount;
        VatAmount = vatAmount;
        CodFee = codFee;
        TotalAmount = Math.Max(0, subtotal - discountTotal + deliveryFee + vatAmount + codFee);
        Notes = notes?.Trim();
        VendorBranchId = vendorBranchId;
        CouponId = couponId;
        
        Status = OrderStatus.PendingPayment;
        PaymentStatus = Zadana.Domain.Modules.Payments.Enums.PaymentStatus.Initiated;
        PlacedAtUtc = DateTime.UtcNow;
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
}
