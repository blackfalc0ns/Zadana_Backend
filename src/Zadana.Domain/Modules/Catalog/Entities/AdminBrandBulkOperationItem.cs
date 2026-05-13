using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;
using System.Text.Json;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class AdminBrandBulkOperationItem : BaseEntity
{
    public Guid OperationId { get; private set; }
    public int RowNumber { get; private set; }
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public Guid CategoryId { get; private set; }
    public string? CategoryIdsJson { get; private set; }
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
        string? coverImageUrl,
        Guid categoryId,
        IReadOnlyList<Guid>? categoryIds,
        bool isActive)
    {
        var resolvedCategoryIds = ResolveCategoryIds(categoryId, categoryIds);

        RowNumber = rowNumber;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        CoverImageUrl = coverImageUrl?.Trim();
        CategoryId = resolvedCategoryIds[0];
        CategoryIdsJson = JsonSerializer.Serialize(resolvedCategoryIds);
        IsActive = isActive;
        Status = AdminBrandBulkOperationItemStatus.Pending;
    }

    public IReadOnlyList<Guid> GetCategoryIds()
    {
        if (!string.IsNullOrWhiteSpace(CategoryIdsJson))
        {
            try
            {
                var categoryIds = JsonSerializer.Deserialize<List<Guid>>(CategoryIdsJson);
                if (categoryIds is { Count: > 0 })
                {
                    return categoryIds.Where(id => id != Guid.Empty).Distinct().ToArray();
                }
            }
            catch (JsonException)
            {
            }
        }

        return CategoryId == Guid.Empty ? [] : [CategoryId];
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

    private static IReadOnlyList<Guid> ResolveCategoryIds(Guid categoryId, IReadOnlyList<Guid>? categoryIds)
    {
        var resolved = categoryIds is { Count: > 0 }
            ? categoryIds
            : [categoryId];

        return resolved
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
    }
}
