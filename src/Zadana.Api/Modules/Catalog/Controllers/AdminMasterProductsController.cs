using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Application.Modules.Catalog.Commands.DeleteMasterProduct;
using Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProductById;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/products")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminMasterProductsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<MasterProductDto>>> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] ProductStatus? status = null)
    {
        var result = await Sender.Send(new GetMasterProductsQuery(searchTerm, categoryId, brandId, status, null, pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MasterProductDto>> GetProduct(Guid id)
    {
        var result = await Sender.Send(new GetMasterProductByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateProduct([FromBody] CreateMasterProductRequest request)
    {
        var command = new CreateMasterProductCommand(
            request.CategoryId,
            request.NameAr,
            request.NameEn,
            request.Slug,
            request.Barcode,
            request.DescriptionAr,
            request.DescriptionEn,
            request.BrandId,
            request.UnitId,
            request.Status,
            request.Images);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateProduct(Guid id, [FromBody] UpdateMasterProductRequest request)
    {
        var command = new UpdateMasterProductCommand(
            id,
            request.CategoryId,
            request.NameAr,
            request.NameEn,
            request.Slug,
            request.Barcode,
            request.DescriptionAr,
            request.DescriptionEn,
            request.BrandId,
            request.UnitId,
            request.Status,
            request.Images);

        await Sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProduct(Guid id)
    {
        await Sender.Send(new DeleteMasterProductCommand(id));
        return NoContent();
    }

    [HttpGet("{id}/vendors")]
    public async Task<ActionResult<PaginatedList<ProductVendorSnapshotDto>>> GetProductVendors(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new Zadana.Application.Modules.Catalog.Queries.GetProductVendors.GetProductVendorsQuery(id, pageNumber, pageSize));
        return Ok(result);
    }
}
