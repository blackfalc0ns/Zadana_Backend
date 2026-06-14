using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;

public record GetBrandProductsQuery(
    Guid BrandId,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    Guid? UnitId = null,
    decimal? MeasurementValue = null,
    Guid? PackageTypeId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Sort = null,
    int Page = 1,
    int PerPage = 20,
    Guid? CustomerId = null,
    Guid? AddressId = null,
    string? City = null) : IRequest<BrandProductsDto>
{
    public GetBrandProductsQuery(
        Guid brandId,
        Guid? categoryId,
        Guid? subcategoryId,
        Guid? unitId,
        decimal? measurementValue,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page = 1,
        int perPage = 20)
        : this(brandId, categoryId, subcategoryId, unitId, measurementValue, null, minPrice, maxPrice, sort, page, perPage)
    {
    }
}
