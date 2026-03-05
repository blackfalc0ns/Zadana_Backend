using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategories;

public record GetCategoriesQuery(bool IncludeInactive = false) : IRequest<List<CategoryDto>>;
