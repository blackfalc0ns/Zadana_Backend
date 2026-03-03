using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryAssignment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid? DriverId { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTime? OfferedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? PickedUpAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public decimal CodAmount { get; private set; }

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

    public void OfferTo(Guid driverId)
    {
        DriverId = driverId;
        Status = AssignmentStatus.OfferSent;
        OfferedAtUtc = DateTime.UtcNow;
    }

    public void Accept()
    {
        Status = AssignmentStatus.Accepted;
        AcceptedAtUtc = DateTime.UtcNow;
    }

    public void Reject()
    {
        DriverId = null;
        Status = AssignmentStatus.Rejected;
    }

    public void MarkPickedUp()
    {
        Status = AssignmentStatus.PickedUp;
        PickedUpAtUtc = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = AssignmentStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = AssignmentStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason.Trim();
    }
}
