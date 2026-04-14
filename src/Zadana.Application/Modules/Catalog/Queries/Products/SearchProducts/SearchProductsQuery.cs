using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;

public record SearchProductsQuery(
    string Query,
    Guid? CategoryId,
    Guid? BrandId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Sort,
    int Page = 1,
    int PerPage = 20) : IRequest<SearchProductsResponseDto>;
