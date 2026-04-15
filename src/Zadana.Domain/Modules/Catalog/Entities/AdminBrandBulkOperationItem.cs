using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class AdminBrandBulkOperationItem : BaseEntity
{
    public Guid OperationId { get; private set; }
    public int RowNumber { get; private set; }
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public AdminBrandBulkOperationItemStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? CreatedBrandId { get; private set; }

    public AdminBrandBulkOperation Operation { get; private set; } = null!;
    public Category Category { get; private set; } = null!;

    private AdminBrandBulkOperationItem() { }

    public AdminBrandBulkOperationItem(
        int rowNumber,
        string nameAr,
        string nameEn,
        string? logoUrl,
        Guid categoryId,
        bool isActive)
    {
        RowNumber = rowNumber;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        CategoryId = categoryId;
        IsActive = isActive;
        Status = AdminBrandBulkOperationItemStatus.Pending;
    }

    public void AttachToOperation(Guid operationId)
    {
        OperationId = operationId;
    }

    public void MarkSucceeded(Guid brandId)
    {
        Status = AdminBrandBulkOperationItemStatus.Succeeded;
        CreatedBrandId = brandId;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AdminBrandBulkOperationItemStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public void MarkSkipped(string errorMessage)
    {
        Status = AdminBrandBulkOperationItemStatus.Skipped;
        ErrorMessage = errorMessage;
    }
}
