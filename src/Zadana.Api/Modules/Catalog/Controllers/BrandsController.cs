using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
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
        [FromQuery(Name = "min_price")] decimal? minPrice,
        [FromQuery(Name = "max_price")] decimal? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new GetBrandProductsQuery(
                brandId,
                categoryId,
                subcategoryId,
                unitId,
                minPrice,
                maxPrice,
                sort,
                page,
                perPage),
            cancellationToken);

        return Ok(result);
    }
}
