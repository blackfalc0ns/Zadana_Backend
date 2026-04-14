using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;
using Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/products")]
[AllowAnonymous]
[Tags("Customer App API")]
public class ProductsController : ApiControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<SearchProductsResponseDto>> SearchProducts(
        [FromQuery(Name = "query")] string query,
        [FromQuery(Name = "category_id")] Guid? categoryId = null,
        [FromQuery(Name = "brand_id")] Guid? brandId = null,
        [FromQuery(Name = "min_price")] decimal? minPrice = null,
        [FromQuery(Name = "max_price")] decimal? maxPrice = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new SearchProductsQuery(query, categoryId, brandId, minPrice, maxPrice, sort, page, perPage),
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
