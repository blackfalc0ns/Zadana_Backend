using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class ImageBank : BaseEntity
{
    public string Url { get; private set; } = null!;
    public string? AltText { get; private set; }
    public string? Tags { get; private set; }
    
    // Approval Workflow
    public ApprovalStatus Status { get; private set; }
    public Guid? UploadedByVendorId { get; private set; }
    public string? RejectionReason { get; private set; }

    // Navigation
    public ICollection<MasterProductImage> ProductUsages { get; private set; } = [];

    private ImageBank() { }

    public ImageBank(string url, string? altText = null, string? tags = null, Guid? uploadedByVendorId = null)
    {
        Url = url.Trim();
        AltText = altText?.Trim();
        Tags = tags?.Trim();
        UploadedByVendorId = uploadedByVendorId;
        
        // If uploaded by vendor, it's pending. If Admin (null), it's approved natively.
        Status = uploadedByVendorId.HasValue ? ApprovalStatus.Pending : ApprovalStatus.Approved;
    }

    public void UpdateMetadata(string? altText, string? tags)
    {
        AltText = altText?.Trim();
        Tags = tags?.Trim();
    }

    public void Approve()
    {
        Status = ApprovalStatus.Approved;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        Status = ApprovalStatus.Rejected;
        RejectionReason = reason;
    }
}
