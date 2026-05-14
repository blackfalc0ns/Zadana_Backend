using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorProfileReviewItem : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Code { get; private set; } = null!;
    public VendorProfileReviewTargetType TargetType { get; private set; }
    public int Step { get; private set; }
    public VendorProfileReviewStatus Status { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTime? LastSubmittedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewedByName { get; private set; }

    public Vendor Vendor { get; private set; } = null!;

    private VendorProfileReviewItem() { }

    public VendorProfileReviewItem(Guid vendorId, string code, VendorProfileReviewTargetType targetType, int step)
    {
        VendorId = vendorId;
        Code = code.Trim();
        TargetType = targetType;
        Step = step;
        Status = VendorProfileReviewStatus.Submitted;
        LastSubmittedAtUtc = DateTime.UtcNow;
    }

    public void MarkSubmitted()
    {
        Status = VendorProfileReviewStatus.Submitted;
        DecisionNote = null;
        ReviewedAtUtc = null;
        ReviewedByUserId = null;
        ReviewedByName = null;
        LastSubmittedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(Guid? reviewedByUserId, string reviewedByName)
    {
        Status = VendorProfileReviewStatus.Approved;
        DecisionNote = null;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewedByUserId;
        ReviewedByName = reviewedByName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string reason, Guid? reviewedByUserId, string reviewedByName)
    {
        Status = VendorProfileReviewStatus.Rejected;
        DecisionNote = reason.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewedByUserId;
        ReviewedByName = reviewedByName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
