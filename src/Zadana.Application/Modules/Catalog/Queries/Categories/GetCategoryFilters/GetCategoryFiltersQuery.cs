using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryFilters;

public record GetCategoryFiltersQuery(Guid CategoryId) : IRequest<CategoryFiltersDto>;
