using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;

public record GetCategoryProductsQuery(
    Guid? CategoryId,
    Guid? ProductTypeId,
    Guid? PartId,
    Guid? QuantityId,
    Guid? BrandId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Sort,
    int Page = 1,
    int PerPage = 20) : IRequest<CategoryProductsDto>;
