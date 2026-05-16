using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;

public record GetCategoryProductsQuery(
    Guid? CategoryId,
    Guid? SubcategoryId,
    Guid? ProductTypeId,
    Guid? PartId,
    Guid? QuantityId,
    Guid? PackageTypeId,
    Guid? BrandId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Sort,
    int Page = 1,
    int PerPage = 20) : IRequest<CategoryProductsDto>
{
    public GetCategoryProductsQuery(
        Guid? categoryId,
        Guid? subcategoryId,
        Guid? productTypeId,
        Guid? partId,
        Guid? quantityId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page = 1,
        int perPage = 20)
        : this(categoryId, subcategoryId, productTypeId, partId, quantityId, null, brandId, minPrice, maxPrice, sort, page, perPage)
    {
    }
}
