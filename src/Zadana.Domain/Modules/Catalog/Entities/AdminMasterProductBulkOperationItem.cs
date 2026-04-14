using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class AdminMasterProductBulkOperationItem : BaseEntity
{
    public Guid OperationId { get; private set; }
    public int RowNumber { get; private set; }
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? Barcode { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid? UnitId { get; private set; }
    public ProductStatus StatusValue { get; private set; }
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public AdminMasterProductBulkOperationItemStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? CreatedMasterProductId { get; private set; }

    public AdminMasterProductBulkOperation Operation { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
    public Brand? Brand { get; private set; }
    public UnitOfMeasure? Unit { get; private set; }

    private AdminMasterProductBulkOperationItem() { }

    public AdminMasterProductBulkOperationItem(
        int rowNumber,
        string nameAr,
        string nameEn,
        string slug,
        string? barcode,
        Guid categoryId,
        Guid? brandId,
        Guid? unitId,
        ProductStatus statusValue,
        string? descriptionAr,
        string? descriptionEn)
    {
        RowNumber = rowNumber;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Slug = slug.Trim();
        Barcode = barcode?.Trim();
        CategoryId = categoryId;
        BrandId = brandId;
        UnitId = unitId;
        StatusValue = statusValue;
        DescriptionAr = descriptionAr?.Trim();
        DescriptionEn = descriptionEn?.Trim();
        Status = AdminMasterProductBulkOperationItemStatus.Pending;
    }

    public void AttachToOperation(Guid operationId)
    {
        OperationId = operationId;
    }

    public void UpdateGeneratedValues(string slug, string? barcode)
    {
        Slug = slug.Trim();
        Barcode = barcode?.Trim();
    }

    public void MarkSucceeded(Guid masterProductId)
    {
        Status = AdminMasterProductBulkOperationItemStatus.Succeeded;
        CreatedMasterProductId = masterProductId;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AdminMasterProductBulkOperationItemStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public void MarkSkipped(string errorMessage)
    {
        Status = AdminMasterProductBulkOperationItemStatus.Skipped;
        ErrorMessage = errorMessage;
    }
}
