using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryAssignment : BaseEntity
{
    private const int OtpLength = 4;

    public Guid OrderId { get; private set; }
    public Guid? DriverId { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTime? OfferedAtUtc { get; private set; }
    public DateTime? OfferExpiresAtUtc { get; private set; }
    public DateTime? OfferRejectedAtUtc { get; private set; }
    public string? OfferRejectedReason { get; private set; }
    public int DispatchAttemptNumber { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? ArrivedAtVendorAtUtc { get; private set; }
    public DateTime? PickedUpAtUtc { get; private set; }
    public DateTime? ArrivedAtCustomerAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public decimal CodAmount { get; private set; }
    public string? PickupOtpCode { get; private set; }
    public DateTime? PickupOtpExpiresAtUtc { get; private set; }
    public DateTime? PickupOtpVerifiedAtUtc { get; private set; }
    public Guid? PickupOtpVerifiedByDriverId { get; private set; }
    public string? DeliveryOtpCode { get; private set; }
    public DateTime? DeliveryOtpExpiresAtUtc { get; private set; }
    public DateTime? DeliveryOtpVerifiedAtUtc { get; private set; }
    public Guid? DeliveryOtpVerifiedByDriverId { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;
    public Driver? Driver { get; private set; }
    public ICollection<DeliveryProof> Proofs { get; private set; } = [];

    private DeliveryAssignment() { }

    public DeliveryAssignment(Guid orderId, decimal codAmount)
    {
        OrderId = orderId;
        CodAmount = codAmount;
        Status = AssignmentStatus.SearchingDriver;
    }

    public void UpdateCodAmount(decimal codAmount)
    {
        CodAmount = Math.Max(0m, codAmount);
    }

    public void OfferTo(Guid driverId, int dispatchAttemptNumber, DateTime offerExpiresAtUtc)
    {
        DriverId = driverId;
        Status = AssignmentStatus.OfferSent;
        OfferedAtUtc = DateTime.UtcNow;
        OfferExpiresAtUtc = offerExpiresAtUtc;
        OfferRejectedAtUtc = null;
        OfferRejectedReason = null;
        DispatchAttemptNumber = dispatchAttemptNumber;
    }

    public void Accept()
    {
        Status = AssignmentStatus.Accepted;
        AcceptedAtUtc = DateTime.UtcNow;
        OfferExpiresAtUtc = null;
        OfferRejectedAtUtc = null;
        OfferRejectedReason = null;
    }

    public void Reject(string? reason)
    {
        EnsureOfferIsActive();
        DriverId = null;
        Status = AssignmentStatus.Rejected;
        OfferRejectedAtUtc = DateTime.UtcNow;
        OfferRejectedReason = string.IsNullOrWhiteSpace(reason) ? "driver-rejected" : reason.Trim();
        OfferExpiresAtUtc = null;
    }

    public void MarkOfferTimedOut()
    {
        EnsureOfferIsActive();
        DriverId = null;
        Status = AssignmentStatus.Rejected;
        OfferRejectedAtUtc = DateTime.UtcNow;
        OfferRejectedReason = "offer-timeout";
        OfferExpiresAtUtc = null;
    }

    public void MarkPickedUp()
    {
        Status = AssignmentStatus.PickedUp;
        PickedUpAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        Status = AssignmentStatus.Cancelled;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason;
        InvalidateOperationalOtps();
    }

    public void MarkArrivedAtVendor()
    {
        Status = AssignmentStatus.ArrivedAtVendor;
        ArrivedAtVendorAtUtc = DateTime.UtcNow;
    }

    public void MarkArrivedAtCustomer()
    {
        Status = AssignmentStatus.ArrivedAtCustomer;
        ArrivedAtCustomerAtUtc = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = AssignmentStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
        DeliveryOtpExpiresAtUtc = null;
    }

    public void Fail(string reason)
    {
        Status = AssignmentStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason.Trim();
        InvalidateOperationalOtps();
    }

    public string EnsurePickupOtp(TimeSpan ttl)
    {
        if (PickupOtpVerifiedAtUtc.HasValue)
        {
            return PickupOtpCode ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(PickupOtpCode))
        {
            PickupOtpCode = GenerateOtp();
        }

        PickupOtpExpiresAtUtc = DateTime.UtcNow.Add(ttl);
        PickupOtpVerifiedAtUtc = null;
        PickupOtpVerifiedByDriverId = null;
        return PickupOtpCode;
    }

    public string EnsureDeliveryOtp(TimeSpan ttl)
    {
        if (DeliveryOtpVerifiedAtUtc.HasValue)
        {
            return DeliveryOtpCode ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(DeliveryOtpCode))
        {
            DeliveryOtpCode = GenerateOtp();
        }

        DeliveryOtpExpiresAtUtc = DateTime.UtcNow.Add(ttl);
        DeliveryOtpVerifiedAtUtc = null;
        DeliveryOtpVerifiedByDriverId = null;
        return DeliveryOtpCode;
    }

    public void VerifyPickupOtp(Guid driverId, string otpCode)
    {
        if (!DriverId.HasValue || DriverId.Value != driverId)
        {
            throw new InvalidOperationException("Only the assigned driver can verify pickup OTP.");
        }

        if (PickupOtpVerifiedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Pickup OTP has already been verified.");
        }

        if (!PickupOtpExpiresAtUtc.HasValue || PickupOtpExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Pickup OTP has expired.");
        }

        if (!string.Equals(PickupOtpCode, NormalizeOtp(otpCode), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pickup OTP is invalid.");
        }

        PickupOtpVerifiedAtUtc = DateTime.UtcNow;
        PickupOtpVerifiedByDriverId = driverId;
        PickupOtpExpiresAtUtc = null;
    }

    public void VerifyDeliveryOtp(Guid driverId, string otpCode)
    {
        if (!DriverId.HasValue || DriverId.Value != driverId)
        {
            throw new InvalidOperationException("Only the assigned driver can verify delivery OTP.");
        }

        if (DeliveryOtpVerifiedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Delivery OTP has already been verified.");
        }

        if (!DeliveryOtpExpiresAtUtc.HasValue || DeliveryOtpExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Delivery OTP has expired.");
        }

        if (!string.Equals(DeliveryOtpCode, NormalizeOtp(otpCode), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Delivery OTP is invalid.");
        }

        DeliveryOtpVerifiedAtUtc = DateTime.UtcNow;
        DeliveryOtpVerifiedByDriverId = driverId;
        DeliveryOtpExpiresAtUtc = null;
    }

    public bool RequiresPickupOtpVerification =>
        (Status == AssignmentStatus.Accepted || Status == AssignmentStatus.ArrivedAtVendor)
        && !PickupOtpVerifiedAtUtc.HasValue;

    public bool RequiresDeliveryOtpVerification => Order.Status == Domain.Modules.Orders.Enums.OrderStatus.OnTheWay && !DeliveryOtpVerifiedAtUtc.HasValue;
    public bool IsPickupOtpVerified => PickupOtpVerifiedAtUtc.HasValue;
    public bool IsDeliveryOtpVerified => DeliveryOtpVerifiedAtUtc.HasValue;

    /// <summary>
    /// True when the driver is at or heading to the vendor and pickup OTP has not been confirmed yet.
    /// Used to determine whether pickupOtpCode should be visible to the driver.
    /// </summary>
    public bool IsInHandoffWindow =>
        (Status == AssignmentStatus.Accepted || Status == AssignmentStatus.ArrivedAtVendor)
        && !PickupOtpVerifiedAtUtc.HasValue;

    public void ResetForRedispatch()
    {
        DriverId = null;
        Status = AssignmentStatus.SearchingDriver;
        OfferedAtUtc = null;
        OfferExpiresAtUtc = null;
        OfferRejectedAtUtc = null;
        OfferRejectedReason = null;
        AcceptedAtUtc = null;
        PickedUpAtUtc = null;
        ArrivedAtVendorAtUtc = null;
        ArrivedAtCustomerAtUtc = null;
        FailedAtUtc = null;
        FailureReason = null;
        DeliveredAtUtc = null;
        DispatchAttemptNumber = 0;
        PickupOtpCode = null;
        PickupOtpExpiresAtUtc = null;
        PickupOtpVerifiedAtUtc = null;
        PickupOtpVerifiedByDriverId = null;
        DeliveryOtpCode = null;
        DeliveryOtpExpiresAtUtc = null;
        DeliveryOtpVerifiedAtUtc = null;
        DeliveryOtpVerifiedByDriverId = null;
    }

    private void EnsureOfferIsActive()
    {
        if (Status != AssignmentStatus.OfferSent)
        {
            throw new InvalidOperationException("An active offer is required.");
        }
    }

    private void InvalidateOperationalOtps()
    {
        PickupOtpExpiresAtUtc = null;
        DeliveryOtpExpiresAtUtc = null;
    }

    private static string NormalizeOtp(string otpCode) =>
        string.Concat((otpCode ?? string.Empty).Where(char.IsDigit));

    private static string GenerateOtp() =>
        Random.Shared.Next((int)Math.Pow(10, OtpLength - 1), (int)Math.Pow(10, OtpLength)).ToString();
}
