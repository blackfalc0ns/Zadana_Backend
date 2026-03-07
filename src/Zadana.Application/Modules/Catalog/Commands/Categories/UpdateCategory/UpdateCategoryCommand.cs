using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;

public record UpdateCategoryCommand(
    Guid Id,
    string NameAr,
    string NameEn,
    string? ImageUrl,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive) : IRequest;
