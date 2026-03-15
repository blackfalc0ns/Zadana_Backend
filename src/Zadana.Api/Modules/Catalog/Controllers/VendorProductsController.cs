using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Commands.CreateVendorProduct;
using Zadana.Application.Modules.Catalog.Commands.VendorProducts.ChangeStatus;
using Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.GetVendorProducts;
using Zadana.Application.Modules.Catalog.Queries.VendorProducts.GetVendorProductById;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/vendor/products")]
[Authorize(Roles = "Vendor")]
public class VendorProductsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentVendorService _currentVendorService;

    public VendorProductsController(ISender sender, ICurrentVendorService currentVendorService)
    {
        _sender = sender;
        _currentVendorService = currentVendorService;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedList<VendorProductDto>>> GetProducts(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? branchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(HttpContext.RequestAborted);
        var query = new GetVendorProductsQuery(vendorId, categoryId, branchId, pageNumber, pageSize);
        var result = await _sender.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VendorProductDto>> GetProductById(Guid id)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(HttpContext.RequestAborted);
        var result = await _sender.Send(new GetVendorProductByIdQuery(vendorId, id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateProduct([FromBody] CreateVendorProductRequest request)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(HttpContext.RequestAborted);
        var command = new CreateVendorProductCommand(
            vendorId,
            request.MasterProductId,
            request.SellingPrice,
            request.CompareAtPrice,
            request.CostPrice,
            request.StockQty,
            request.MinOrderQty,
            request.MaxOrderQty,
            request.Sku,
            request.BranchId
        );
        var result = await _sender.Send(command);
        return CreatedAtAction(nameof(GetProductById), new { id = result }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateProduct(Guid id, [FromBody] UpdateVendorProductRequest request)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(HttpContext.RequestAborted);
        var command = new UpdateVendorProductCommand(
            id,
            vendorId,
            request.SellingPrice,
            request.CompareAtPrice,
            request.StockQty,
            request.CustomNameAr,
            request.CustomNameEn,
            request.CustomDescriptionAr,
            request.CustomDescriptionEn
        );
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult> ChangeStatus(Guid id, [FromBody] ChangeProductStatusRequest request)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(HttpContext.RequestAborted);
        var command = new ChangeVendorProductStatusCommand(id, vendorId, request.IsActive);
        await _sender.Send(command);
        return NoContent();
    }
}

public record CreateVendorProductRequest(
    Guid MasterProductId,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    decimal? CostPrice,
    int StockQty,
    int MinOrderQty,
    int? MaxOrderQty,
    string? Sku,
    Guid? BranchId);

public record UpdateVendorProductRequest(
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int StockQty,
    string? CustomNameAr,
    string? CustomNameEn,
    string? CustomDescriptionAr,
    string? CustomDescriptionEn);

public record ChangeProductStatusRequest(bool IsActive);

