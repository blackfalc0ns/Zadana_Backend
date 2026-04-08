using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryFilters;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategorySubcategories;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/categories")]
[AllowAnonymous]
[Tags("Customer App API")]
public class CategoriesController : ApiControllerBase
{
    [HttpGet("{categoryId:guid}/products")]
    public async Task<ActionResult<CategoryProductsDto>> GetProducts(
        Guid categoryId,
        [FromQuery(Name = "quantity_id")] Guid? quantityId,
        [FromQuery(Name = "brand_id")] Guid? brandId,
        [FromQuery(Name = "min_price")] decimal? minPrice,
        [FromQuery(Name = "max_price")] decimal? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new GetCategoryProductsQuery(
                categoryId,
                quantityId,
                brandId,
                minPrice,
                maxPrice,
                sort,
                page,
                perPage),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{categoryId:guid}/filters")]
    public async Task<ActionResult<CategoryFiltersDto>> GetFilters(Guid categoryId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCategoryFiltersQuery(categoryId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{categoryId:guid}/subcategories")]
    public async Task<ActionResult<List<CategoryListItemDto>>> GetSubcategories(Guid categoryId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCategorySubcategoriesQuery(categoryId), cancellationToken);
        return Ok(result);
    }
}
