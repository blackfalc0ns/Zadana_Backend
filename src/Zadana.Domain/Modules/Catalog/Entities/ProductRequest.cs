using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class ProductRequest : BaseEntity
{
    public Guid VendorId { get; private set; }
    
    public string SuggestedNameAr { get; private set; } = null!;
    public string SuggestedNameEn { get; private set; } = null!;
    public string? SuggestedDescriptionAr { get; private set; }
    public string? SuggestedDescriptionEn { get; private set; }
    public Guid SuggestedCategoryId { get; private set; }
    
    public string? ImageUrl { get; private set; }
    
    public ApprovalStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    // Navigation
    public Vendor Vendor { get; private set; } = null!;
    public Category Category { get; private set; } = null!;

    private ProductRequest() { }

    public ProductRequest(
        Guid vendorId,
        string suggestedNameAr,
        string suggestedNameEn,
        Guid suggestedCategoryId,
        string? suggestedDescriptionAr = null,
        string? suggestedDescriptionEn = null,
        string? imageUrl = null)
    {
        VendorId = vendorId;
        SuggestedNameAr = suggestedNameAr.Trim();
        SuggestedNameEn = suggestedNameEn.Trim();
        SuggestedCategoryId = suggestedCategoryId;
        SuggestedDescriptionAr = suggestedDescriptionAr?.Trim();
        SuggestedDescriptionEn = suggestedDescriptionEn?.Trim();
        ImageUrl = imageUrl?.Trim();
        Status = ApprovalStatus.Pending;
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
