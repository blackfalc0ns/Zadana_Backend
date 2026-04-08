using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;

public record GetBrandProductsQuery(
    Guid BrandId,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    Guid? UnitId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Sort = null,
    int Page = 1,
    int PerPage = 20) : IRequest<BrandProductsDto>;
