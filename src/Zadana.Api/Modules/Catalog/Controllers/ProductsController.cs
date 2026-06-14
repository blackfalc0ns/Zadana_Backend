using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Zadana.Api.Configuration;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;
using Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/products")]
[AllowAnonymous]
[Tags("Customer App API")]
public class ProductsController : ApiControllerBase
{
    private readonly ICurrentUserService? _currentUserService;

    public ProductsController(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet("search")]
    [OutputCache(PolicyName = OutputCachePolicyNames.PublicCatalogBrowse)]
    public async Task<ActionResult<SearchProductsResponseDto>> SearchProducts(
        [FromQuery(Name = "query")] string? query = null,
        [FromQuery(Name = "category_id")] Guid? categoryId = null,
        [FromQuery(Name = "brand_id")] Guid? brandId = null,
        [FromQuery(Name = "min_price")] decimal? minPrice = null,
        [FromQuery(Name = "max_price")] decimal? maxPrice = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery(Name = "address_id")] Guid? addressId = null,
        [FromQuery(Name = "city")] string? city = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new SearchProductsQuery(
                query,
                categoryId,
                brandId,
                minPrice,
                maxPrice,
                sort,
                page,
                perPage,
                _currentUserService?.UserId,
                addressId,
                city),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{productId:guid}")]
    public async Task<ActionResult<ProductDetailsDto>> GetProduct(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetProductDetailsQuery(productId), cancellationToken);
        return Ok(result);
    }
}
