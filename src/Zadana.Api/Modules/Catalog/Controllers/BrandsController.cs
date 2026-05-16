using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Zadana.Api.Controllers;
using Zadana.Api.Configuration;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandById;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetCustomerBrands;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/brands")]
[AllowAnonymous]
[Tags("Customer App API")]
public class BrandsController : ApiControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = OutputCachePolicyNames.CatalogMetadata)]
    public async Task<ActionResult<List<BrandCustomerDto>>> GetBrands(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCustomerBrandsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{brandId:guid}")]
    public async Task<ActionResult<BrandCustomerDto>> GetBrandById(Guid brandId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetBrandByIdQuery(brandId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{brandId:guid}/filters")]
    [OutputCache(PolicyName = OutputCachePolicyNames.CatalogMetadata)]
    public async Task<ActionResult<BrandFiltersDto>> GetFilters(Guid brandId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetBrandFiltersQuery(brandId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{brandId:guid}/products")]
    public async Task<ActionResult<BrandProductsDto>> GetProducts(
        Guid brandId,
        [FromQuery(Name = "category_id")] Guid? categoryId,
        [FromQuery(Name = "subcategory_id")] Guid? subcategoryId,
        [FromQuery(Name = "unit_id")] Guid? unitId,
        [FromQuery(Name = "measurement_unit_id")] Guid? measurementUnitId,
        [FromQuery(Name = "measurement_value")] decimal? measurementValue,
        [FromQuery(Name = "min_price")] decimal? minPrice,
        [FromQuery(Name = "max_price")] decimal? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery(Name = "package_type_id")] Guid? packageTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveMeasurementUnitId = measurementUnitId ?? unitId;

        var result = await Sender.Send(
            new GetBrandProductsQuery(
                brandId,
                categoryId,
                subcategoryId,
                effectiveMeasurementUnitId,
                measurementValue,
                packageTypeId,
                minPrice,
                maxPrice,
                sort,
                page,
                perPage),
            cancellationToken);

        return Ok(result);
    }

    [NonAction]
    public Task<ActionResult<BrandProductsDto>> GetProducts(
        Guid brandId,
        Guid? categoryId,
        Guid? subcategoryId,
        Guid? unitId,
        decimal? measurementValue,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page,
        int perPage,
        CancellationToken cancellationToken) =>
        GetProducts(brandId, categoryId, subcategoryId, unitId, null, measurementValue, minPrice, maxPrice, sort, page, perPage, null, cancellationToken);
}
