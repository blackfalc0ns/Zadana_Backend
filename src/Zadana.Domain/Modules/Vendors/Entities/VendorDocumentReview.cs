using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorDocumentReview : BaseEntity
{
    public Guid VendorId { get; private set; }
    public VendorDocumentType Type { get; private set; }
    public VendorDocumentReviewDecision Decision { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewedByName { get; private set; }

    public Vendor Vendor { get; private set; } = null!;

    private VendorDocumentReview() { }

    public VendorDocumentReview(Guid vendorId, VendorDocumentType type)
    {
        VendorId = vendorId;
        Type = type;
        Decision = VendorDocumentReviewDecision.Pending;
    }

    public void Approve(Guid? reviewedByUserId, string reviewedByName)
    {
        Decision = VendorDocumentReviewDecision.Approved;
        RejectionReason = null;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewedByUserId;
        ReviewedByName = reviewedByName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string rejectionReason, Guid? reviewedByUserId, string reviewedByName)
    {
        Decision = VendorDocumentReviewDecision.Rejected;
        RejectionReason = rejectionReason.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewedByUserId;
        ReviewedByName = reviewedByName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ResetToPending()
    {
        Decision = VendorDocumentReviewDecision.Pending;
        RejectionReason = null;
        ReviewedAtUtc = null;
        ReviewedByUserId = null;
        ReviewedByName = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
