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
    public Guid? SuggestedCategoryId { get; private set; }
    public Guid? SuggestedCategoryRequestId { get; private set; }
    public Guid? SuggestedBrandId { get; private set; }
    public Guid? SuggestedBrandRequestId { get; private set; }
    public Guid? SuggestedUnitOfMeasureId { get; private set; }
    public string? ImageUrl { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewedBy { get; private set; }
    public Guid? CreatedMasterProductId { get; private set; }

    // Navigation
    public Vendor Vendor { get; private set; } = null!;
    public Category? Category { get; private set; }
    public CategoryRequest? CategoryRequest { get; private set; }
    public Brand? Brand { get; private set; }
    public BrandRequest? BrandRequest { get; private set; }
    public UnitOfMeasure? UnitOfMeasure { get; private set; }
    public MasterProduct? CreatedMasterProduct { get; private set; }

    private ProductRequest() { }

    public ProductRequest(
        Guid vendorId,
        string suggestedNameAr,
        string suggestedNameEn,
        Guid? suggestedCategoryId = null,
        Guid? suggestedCategoryRequestId = null,
        Guid? suggestedBrandId = null,
        Guid? suggestedBrandRequestId = null,
        Guid? suggestedUnitOfMeasureId = null,
        string? suggestedDescriptionAr = null,
        string? suggestedDescriptionEn = null,
        string? imageUrl = null)
    {
        VendorId = vendorId;
        SuggestedNameAr = suggestedNameAr.Trim();
        SuggestedNameEn = suggestedNameEn.Trim();
        SuggestedCategoryId = suggestedCategoryId;
        SuggestedCategoryRequestId = suggestedCategoryRequestId;
        SuggestedBrandId = suggestedBrandId;
        SuggestedBrandRequestId = suggestedBrandRequestId;
        SuggestedUnitOfMeasureId = suggestedUnitOfMeasureId;
        SuggestedDescriptionAr = suggestedDescriptionAr?.Trim();
        SuggestedDescriptionEn = suggestedDescriptionEn?.Trim();
        ImageUrl = imageUrl?.Trim();
        Status = ApprovalStatus.Pending;
    }

    public void Approve(string reviewedBy, Guid? createdMasterProductId = null)
    {
        Status = ApprovalStatus.Approved;
        RejectionReason = null;
        ReviewedBy = reviewedBy.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        CreatedMasterProductId = createdMasterProductId;
    }

    public void Reject(string reason, string reviewedBy)
    {
        Status = ApprovalStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedBy = reviewedBy.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
    }

    public void LinkCreatedMasterProduct(Guid masterProductId)
    {
        CreatedMasterProductId = masterProductId;
    }
}
