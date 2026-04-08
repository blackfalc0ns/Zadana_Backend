using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;

public record GetBrandFiltersQuery(Guid BrandId) : IRequest<BrandFiltersDto>;
