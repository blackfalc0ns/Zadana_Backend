using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DriverDocumentReview : BaseEntity
{
    public Guid DriverId { get; private set; }
    public DriverDocumentType Type { get; private set; }
    public DriverDocumentReviewDecision Decision { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewedByName { get; private set; }

    public Driver Driver { get; private set; } = null!;

    private DriverDocumentReview() { }

    public DriverDocumentReview(Guid driverId, DriverDocumentType type)
    {
        DriverId = driverId;
        Type = type;
        Decision = DriverDocumentReviewDecision.Pending;
    }

    public void Approve(Guid? reviewedByUserId, string reviewedByName)
    {
        Decision = DriverDocumentReviewDecision.Approved;
        RejectionReason = null;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewedByUserId;
        ReviewedByName = reviewedByName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string rejectionReason, Guid? reviewedByUserId, string reviewedByName)
    {
        Decision = DriverDocumentReviewDecision.Rejected;
        RejectionReason = rejectionReason.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewedByUserId;
        ReviewedByName = reviewedByName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ResetToPending()
    {
        Decision = DriverDocumentReviewDecision.Pending;
        RejectionReason = null;
        ReviewedAtUtc = null;
        ReviewedByUserId = null;
        ReviewedByName = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
