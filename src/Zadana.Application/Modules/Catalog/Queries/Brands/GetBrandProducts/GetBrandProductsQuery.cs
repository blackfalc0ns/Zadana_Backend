using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;

public record GetBrandProductsQuery(
    Guid BrandId,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    Guid? UnitId = null,
    Guid? PackageTypeId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Sort = null,
    int Page = 1,
    int PerPage = 20) : IRequest<BrandProductsDto>
{
    public GetBrandProductsQuery(
        Guid brandId,
        Guid? categoryId,
        Guid? subcategoryId,
        Guid? unitId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page = 1,
        int perPage = 20)
        : this(brandId, categoryId, subcategoryId, unitId, null, minPrice, maxPrice, sort, page, perPage)
    {
    }
}
