namespace Zadana.Application.Modules.Catalog.DTOs;

public record CategoryDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string? ImageUrl,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive,
    string? ParentNameAr = null,
    string? ParentNameEn = null,
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null,
    int? MasterProductsCount = 0,
    int Level = 0,
    List<CategoryDto>? SubCategories = null);
