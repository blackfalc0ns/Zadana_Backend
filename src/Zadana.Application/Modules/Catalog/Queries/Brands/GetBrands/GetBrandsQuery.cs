using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrands;

public record GetBrandsQuery(bool IncludeInactive = false) : IRequest<List<BrandDto>>;
