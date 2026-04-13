using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategorySubcategories;

public record GetCategorySubcategoriesQuery(Guid? CategoryId = null) : IRequest<List<CategoryListItemDto>>;
