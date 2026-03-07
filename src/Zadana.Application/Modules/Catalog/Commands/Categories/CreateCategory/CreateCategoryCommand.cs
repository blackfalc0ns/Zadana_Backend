using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;

public record CreateCategoryCommand(
    string NameAr,
    string NameEn,
    string? ImageUrl,
    Guid? ParentCategoryId,
    int DisplayOrder) : IRequest<CategoryDto>;
