using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class CategoryRequest : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? ImageUrl { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public int DisplayOrder { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewedBy { get; private set; }
    public Guid? CreatedCategoryId { get; private set; }

    public Vendor Vendor { get; private set; } = null!;
    public Category? ParentCategory { get; private set; }
    public Category? CreatedCategory { get; private set; }

    private CategoryRequest() { }

    public CategoryRequest(
        Guid vendorId,
        string nameAr,
        string nameEn,
        Guid? parentCategoryId = null,
        int displayOrder = 1,
        string? imageUrl = null)
    {
        VendorId = vendorId;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        ParentCategoryId = parentCategoryId;
        DisplayOrder = displayOrder < 1 ? 1 : displayOrder;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        Status = ApprovalStatus.Pending;
    }

    public void Approve(string reviewedBy, Guid createdCategoryId)
    {
        Status = ApprovalStatus.Approved;
        RejectionReason = null;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedBy = reviewedBy.Trim();
        CreatedCategoryId = createdCategoryId;
    }

    public void Reject(string reason, string reviewedBy)
    {
        Status = ApprovalStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedBy = reviewedBy.Trim();
    }
}
