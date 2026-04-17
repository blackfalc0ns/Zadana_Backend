using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class BrandRequest : BaseEntity
{
    public Guid VendorId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewedBy { get; private set; }
    public Guid? CreatedBrandId { get; private set; }

    public Vendor Vendor { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
    public Brand? CreatedBrand { get; private set; }

    private BrandRequest() { }

    public BrandRequest(Guid vendorId, Guid categoryId, string nameAr, string nameEn, string? logoUrl = null)
    {
        VendorId = vendorId;
        CategoryId = categoryId;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        Status = ApprovalStatus.Pending;
    }

    public void Approve(string reviewedBy, Guid createdBrandId)
    {
        Status = ApprovalStatus.Approved;
        RejectionReason = null;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedBy = reviewedBy.Trim();
        CreatedBrandId = createdBrandId;
    }

    public void Reject(string reason, string reviewedBy)
    {
        Status = ApprovalStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedBy = reviewedBy.Trim();
    }
}
