using System.Security.Cryptography;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class Order : BaseEntity
{
    private const int PickupOtpLength = 4;

    public string OrderNumber { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid? VendorBranchId { get; private set; }
    /// <summary>Required for delivery; null for customer-pickup orders.</summary>
    public Guid? CustomerAddressId { get; private set; }
    public Guid? CouponId { get; private set; }

    public FulfillmentType Fulfillment { get; private set; } = FulfillmentType.Delivery;
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
    public DateTime? ReadyForPickupAtUtc { get; private set; }
    public DateTime? ConvertedToDeliveryAtUtc { get; private set; }
    public Guid? DeliveryUpgradePaymentId { get; private set; }
    public DateTime? PickupNoShowDeadlineUtc { get; private set; }
    public bool PickupReminder50Sent { get; private set; }
    public bool PickupReminder90Sent { get; private set; }

    // Customer ↔ merchant pickup OTP (not the driver assignment OTP).
    public string? PickupOtpCode { get; private set; }
    public DateTime? PickupOtpExpiresAtUtc { get; private set; }
    public DateTime? PickupOtpVerifiedAtUtc { get; private set; }
    public Guid? PickupOtpVerifiedByVendorUserId { get; private set; }
    public int PickupOtpFailedAttempts { get; private set; }
    public DateTime? PickupOtpLockedUntilUtc { get; private set; }
    public int PickupOtpResendCount { get; private set; }
    public DateTime? PickupOtpResendWindowStartedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

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
    public ICollection<OrderCancellationRequest> CancellationRequests { get; private set; } = [];

    private Order() { }

    public Order(
        string orderNumber,
        Guid userId,
        Guid vendorId,
        Guid? customerAddressId,
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
        Guid? couponId = null,
        FulfillmentType fulfillment = FulfillmentType.Delivery)
    {
        if (fulfillment == FulfillmentType.Delivery && !customerAddressId.HasValue)
        {
            throw new BusinessRuleException("CUSTOMER_ADDRESS_REQUIRED", "Delivery orders require a customer address.");
        }

        if (fulfillment == FulfillmentType.Pickup && !vendorBranchId.HasValue)
        {
            throw new BusinessRuleException("PICKUP_BRANCH_REQUIRED", "Pickup orders require a vendor branch.");
        }

        OrderNumber = orderNumber;
        UserId = userId;
        VendorId = vendorId;
        CustomerAddressId = customerAddressId;
        Fulfillment = fulfillment;
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

        ProductGross = subtotal;
        ProductNet = Math.Max(0, subtotal - discountTotal);
        VendorCommissionAmount = commissionAmount;
        DriverCommissionAmount = 0m;
        Currency = "SAR";
        PricingMode = "live";
        TaxPolicySnapshot = null;
        CommissionPolicySnapshot = null;

        Status = OrderStatus.PendingPayment;
        PaymentStatus = PaymentStatus.Initiated;
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
        if (newStatus == OrderStatus.ReadyForPickup && !ReadyForPickupAtUtc.HasValue)
        {
            ReadyForPickupAtUtc = DateTime.UtcNow;
        }

        StatusHistory.Add(new OrderStatusHistory(Id, newStatus, changedByUserId, note, oldStatus));
    }

    public void UpdatePaymentStatus(PaymentStatus newStatus)
    {
        PaymentStatus = newStatus;
        if (newStatus == PaymentStatus.Paid && Status == OrderStatus.PendingPayment)
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

    public bool IsPickup => Fulfillment == FulfillmentType.Pickup;

    public string MarkReadyForCustomerPickup(TimeSpan otpTtl, TimeSpan noShowTimeout)
    {
        if (Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException("PICKUP_OTP_NOT_APPLICABLE", "Pickup OTP applies only to pickup orders.");
        }

        if (Status != OrderStatus.ReadyForPickup)
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                "Pickup OTP can only be generated when the order is ReadyForPickup.");
        }

        ReadyForPickupAtUtc ??= DateTime.UtcNow;
        PickupNoShowDeadlineUtc = ReadyForPickupAtUtc.Value.Add(noShowTimeout);
        PickupReminder50Sent = false;
        PickupReminder90Sent = false;
        return EnsureCustomerPickupOtp(otpTtl);
    }

    public string EnsureCustomerPickupOtp(TimeSpan ttl)
    {
        if (PickupOtpVerifiedAtUtc.HasValue)
        {
            return PickupOtpCode ?? string.Empty;
        }

        var isMissing = string.IsNullOrWhiteSpace(PickupOtpCode);
        var isExpired = !PickupOtpExpiresAtUtc.HasValue || PickupOtpExpiresAtUtc.Value <= DateTime.UtcNow;
        if (isMissing || isExpired)
        {
            PickupOtpCode = GeneratePickupOtp();
            PickupOtpFailedAttempts = 0;
            PickupOtpLockedUntilUtc = null;
        }

        PickupOtpExpiresAtUtc = DateTime.UtcNow.Add(ttl);
        return PickupOtpCode!;
    }

    public string RegenerateCustomerPickupOtp(TimeSpan ttl, int maxResendsPerHour = 3)
    {
        if (Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException("PICKUP_OTP_NOT_APPLICABLE", "Pickup OTP applies only to pickup orders.");
        }

        if (PickupOtpVerifiedAtUtc.HasValue)
        {
            throw new BusinessRuleException("PICKUP_OTP_ALREADY_VERIFIED", "Pickup OTP has already been verified.");
        }

        if (Status != OrderStatus.ReadyForPickup)
        {
            throw new BusinessRuleException("PICKUP_OTP_NOT_READY", "Pickup OTP can only be resent while ReadyForPickup.");
        }

        var now = DateTime.UtcNow;
        if (!PickupOtpResendWindowStartedAtUtc.HasValue ||
            now - PickupOtpResendWindowStartedAtUtc.Value > TimeSpan.FromHours(1))
        {
            PickupOtpResendWindowStartedAtUtc = now;
            PickupOtpResendCount = 0;
        }

        if (PickupOtpResendCount >= maxResendsPerHour)
        {
            throw new BusinessRuleException(
                "PICKUP_OTP_RESEND_RATE_LIMIT",
                "Pickup OTP resend rate limit exceeded. Try again later.");
        }

        PickupOtpCode = GeneratePickupOtp();
        PickupOtpExpiresAtUtc = now.Add(ttl);
        PickupOtpFailedAttempts = 0;
        PickupOtpLockedUntilUtc = null;
        PickupOtpResendCount++;
        return PickupOtpCode;
    }

    public void VerifyCustomerPickupOtp(
        Guid vendorUserId,
        string otpCode,
        int maxAttempts,
        int lockoutMinutes)
    {
        if (Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException("PICKUP_OTP_NOT_APPLICABLE", "Pickup OTP applies only to pickup orders.");
        }

        if (Status != OrderStatus.ReadyForPickup)
        {
            throw new BusinessRuleException(
                "ORDER_STATE_CHANGED_RETRY",
                "Order is no longer ready for pickup verification.");
        }

        if (PickupOtpVerifiedAtUtc.HasValue)
        {
            throw new BusinessRuleException("PICKUP_OTP_ALREADY_VERIFIED", "Pickup OTP has already been verified.");
        }

        var now = DateTime.UtcNow;
        if (PickupOtpLockedUntilUtc.HasValue && PickupOtpLockedUntilUtc.Value > now)
        {
            throw new BusinessRuleException("PICKUP_OTP_LOCKED", "Pickup OTP verification is temporarily locked.");
        }

        if (!PickupOtpExpiresAtUtc.HasValue || PickupOtpExpiresAtUtc.Value <= now)
        {
            throw new BusinessRuleException("PICKUP_OTP_INVALID", "Pickup OTP has expired.");
        }

        if (!string.Equals(PickupOtpCode, NormalizePickupOtp(otpCode), StringComparison.Ordinal))
        {
            PickupOtpFailedAttempts++;
            if (PickupOtpFailedAttempts >= Math.Max(1, maxAttempts))
            {
                PickupOtpLockedUntilUtc = now.AddMinutes(Math.Max(1, lockoutMinutes));
                PickupOtpFailedAttempts = 0;
            }

            throw new BusinessRuleException("PICKUP_OTP_INVALID", "Pickup OTP is invalid.");
        }

        PickupOtpVerifiedAtUtc = now;
        PickupOtpVerifiedByVendorUserId = vendorUserId;
        PickupOtpExpiresAtUtc = null;
        PickupOtpFailedAttempts = 0;
        PickupOtpLockedUntilUtc = null;
        ChangeStatus(OrderStatus.Delivered, vendorUserId, "Customer pickup confirmed via OTP");
    }

    public void InvalidateCustomerPickupOtp()
    {
        PickupOtpCode = null;
        PickupOtpExpiresAtUtc = null;
        PickupOtpVerifiedAtUtc = null;
        PickupOtpVerifiedByVendorUserId = null;
        PickupOtpFailedAttempts = 0;
        PickupOtpLockedUntilUtc = null;
        PickupNoShowDeadlineUtc = null;
        PickupReminder50Sent = false;
        PickupReminder90Sent = false;
    }

    public void ExtendPickupNoShowDeadline(DateTime newDeadlineUtc)
    {
        if (Fulfillment != FulfillmentType.Pickup || Status != OrderStatus.ReadyForPickup)
        {
            throw new BusinessRuleException(
                "PICKUP_DEADLINE_NOT_EXTENDABLE",
                "Pickup no-show deadline can only be extended for ready pickup orders.");
        }

        PickupNoShowDeadlineUtc = newDeadlineUtc;
        PickupReminder50Sent = false;
        PickupReminder90Sent = false;
        if (PickupOtpExpiresAtUtc.HasValue && PickupOtpExpiresAtUtc.Value < newDeadlineUtc)
        {
            PickupOtpExpiresAtUtc = newDeadlineUtc;
        }
    }

    public void MarkPickupReminder50Sent() => PickupReminder50Sent = true;
    public void MarkPickupReminder90Sent() => PickupReminder90Sent = true;

    public void AttachDeliveryUpgradePayment(Guid paymentId)
    {
        if (Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException(
                "CONVERT_TO_DELIVERY_NOT_ALLOWED_IN_STATE",
                "Only pickup orders can attach a delivery upgrade payment.");
        }

        if (ConvertedToDeliveryAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "CONVERT_TO_DELIVERY_NOT_ALLOWED_IN_STATE",
                "Order was already converted to delivery.");
        }

        DeliveryUpgradePaymentId = paymentId;
    }

    public void ConvertToDelivery(
        Guid customerAddressId,
        decimal newDeliveryFee,
        decimal baseDeliveryFee,
        decimal distanceDeliveryFee,
        decimal surgeDeliveryFee,
        Guid? changedByUserId,
        ConvertToDeliveryReason reason)
    {
        if (Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException(
                "CONVERT_TO_DELIVERY_NOT_ALLOWED_IN_STATE",
                "Only pickup orders can be converted to delivery.");
        }

        if (ConvertedToDeliveryAtUtc.HasValue)
        {
            return; // idempotent
        }

        var allowed = Status is OrderStatus.Placed
            or OrderStatus.PendingVendorAcceptance
            or OrderStatus.Accepted
            or OrderStatus.Preparing
            or OrderStatus.ReadyForPickup;

        if (!allowed)
        {
            throw new BusinessRuleException(
                "CONVERT_TO_DELIVERY_NOT_ALLOWED_IN_STATE",
                $"Cannot convert to delivery while order is {Status}.");
        }

        if (customerAddressId == Guid.Empty)
        {
            throw new BusinessRuleException("CUSTOMER_ADDRESS_REQUIRED", "Customer address is required for delivery.");
        }

        if (newDeliveryFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newDeliveryFee));
        }

        var wasReadyForPickup = Status == OrderStatus.ReadyForPickup;
        Fulfillment = FulfillmentType.Delivery;
        CustomerAddressId = customerAddressId;
        DeliveryFee = newDeliveryFee;
        BaseDeliveryFee = baseDeliveryFee;
        DistanceDeliveryFee = distanceDeliveryFee;
        SurgeDeliveryFee = surgeDeliveryFee;
        TotalAmount = Math.Max(0, Subtotal - DiscountTotal + DeliveryFee + VatAmount + CodFee);
        ConvertedToDeliveryAtUtc = DateTime.UtcNow;
        InvalidateCustomerPickupOtp();

        if (wasReadyForPickup)
        {
            ChangeStatus(
                OrderStatus.Preparing,
                changedByUserId,
                $"Converted to delivery ({reason}); rolled back from ReadyForPickup");
        }
        else
        {
            StatusHistory.Add(new OrderStatusHistory(
                Id,
                Status,
                changedByUserId,
                $"Converted pickup order to delivery ({reason})",
                Status));
        }
    }

    public void ApplyDeliveryFeeDeltaPaid(decimal deltaAmount)
    {
        if (deltaAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaAmount));
        }

        // Total already recomputed in ConvertToDelivery; keep hook for ledger callers.
        _ = deltaAmount;
    }

    private static string GeneratePickupOtp()
    {
        var maxExclusive = (int)Math.Pow(10, PickupOtpLength);
        var value = RandomNumberGenerator.GetInt32(0, maxExclusive);
        return value.ToString($"D{PickupOtpLength}");
    }

    private static string NormalizePickupOtp(string? otpCode) =>
        (otpCode ?? string.Empty).Trim();
}
