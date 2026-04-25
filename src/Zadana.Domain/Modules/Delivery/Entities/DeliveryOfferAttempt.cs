using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryOfferAttempt : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid DriverId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DeliveryOfferAttemptStatus Status { get; private set; }
    public DateTime OfferedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    private DeliveryOfferAttempt() { }

    public DeliveryOfferAttempt(
        Guid orderId,
        Guid? assignmentId,
        Guid driverId,
        int attemptNumber,
        DateTime expiresAtUtc)
    {
        OrderId = orderId;
        AssignmentId = assignmentId;
        DriverId = driverId;
        AttemptNumber = attemptNumber;
        Status = DeliveryOfferAttemptStatus.Offered;
        OfferedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void MarkAccepted()
    {
        Status = DeliveryOfferAttemptStatus.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
        RejectionReason = null;
    }

    public void MarkRejected(string? reason)
    {
        Status = DeliveryOfferAttemptStatus.Rejected;
        RespondedAtUtc = DateTime.UtcNow;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? "driver-rejected" : reason.Trim();
    }

    public void MarkTimedOut()
    {
        Status = DeliveryOfferAttemptStatus.TimedOut;
        RespondedAtUtc = DateTime.UtcNow;
        RejectionReason = "offer-timeout";
    }
}
