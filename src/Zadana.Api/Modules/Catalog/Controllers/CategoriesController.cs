using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Zadana.Api.Controllers;
using Zadana.Api.Configuration;
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
    [HttpGet("subcategories")]
    [HttpGet("{categoryId:guid}/subcategories")]
    [OutputCache(PolicyName = OutputCachePolicyNames.CatalogMetadata)]
    public async Task<ActionResult<List<CategoryListItemDto>>> GetSubcategories(
        [FromQuery(Name = "categoryId")] Guid? queryCategoryId,
        [FromRoute] Guid? categoryId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveCategoryId = categoryId ?? queryCategoryId;
        var result = await Sender.Send(new GetCategorySubcategoriesQuery(effectiveCategoryId, limit), cancellationToken);
        return Ok(result);
    }

    [HttpGet("products")]
    [HttpGet("{categoryId:guid}/products")]
    [OutputCache(PolicyName = OutputCachePolicyNames.PublicCatalogBrowse)]
    public async Task<ActionResult<CategoryProductsDto>> GetProducts(
        [FromQuery(Name = "categoryId")] Guid? queryCategoryId,
        [FromRoute] Guid? categoryId,
        [FromQuery(Name = "subcategory_id")] Guid? subcategoryId,
        [FromQuery(Name = "product_type_id")] Guid? productTypeId,
        [FromQuery(Name = "part_id")] Guid? partId,
        [FromQuery(Name = "quantity_id")] Guid? quantityId,
        [FromQuery(Name = "measurement_unit_id")] Guid? measurementUnitId,
        [FromQuery(Name = "measurement_value")] decimal? measurementValue,
        [FromQuery(Name = "brand_id")] Guid? brandId,
        [FromQuery(Name = "min_price")] decimal? minPrice,
        [FromQuery(Name = "max_price")] decimal? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery(Name = "package_type_id")] Guid? packageTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveCategoryId = categoryId ?? queryCategoryId;
        var effectiveMeasurementUnitId = measurementUnitId ?? quantityId;

        var result = await Sender.Send(
            new GetCategoryProductsQuery(
                effectiveCategoryId,
                subcategoryId,
                productTypeId,
                partId,
                effectiveMeasurementUnitId,
                measurementValue,
                packageTypeId,
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
    [OutputCache(PolicyName = OutputCachePolicyNames.CatalogMetadata)]
    public async Task<ActionResult<CategoryFiltersDto>> GetFilters(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCategoryFiltersQuery(categoryId), cancellationToken);
        return Ok(result);
    }
}
