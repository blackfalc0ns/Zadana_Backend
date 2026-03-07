namespace Zadana.Application.Modules.Catalog.DTOs;

public record CategoryDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string? ImageUrl,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive,
    List<CategoryDto>? SubCategories = null);
