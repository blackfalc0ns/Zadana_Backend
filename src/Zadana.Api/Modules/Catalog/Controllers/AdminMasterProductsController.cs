using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Commands.AdminMasterProducts.BulkCreateMasterProducts;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Application.Modules.Catalog.Commands.DeleteMasterProduct;
using Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProductById;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;
using Zadana.Application.Modules.Catalog.Queries.AdminMasterProducts.GetAdminMasterProductBulkOperation;
using Zadana.Application.Modules.Catalog.Queries.AdminMasterProducts.GetAdminMasterProductBulkOperationItems;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/products")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Tags("Catalog (Admins)")]
public class AdminMasterProductsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public AdminMasterProductsController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

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

    [HttpPost("search")]
    public async Task<ActionResult<CatalogSearchResponse<MasterProductDto, ProductSearchFiltersDto, ProductSearchFacetsDto>>> SearchProducts([FromBody] ProductSearchRequest? request)
    {
        var pagination = request?.Pagination ?? new CatalogPaginationRequest();
        var filters = request?.Filters;

        var result = await Sender.Send(new SearchMasterProductsQuery(
            request?.Search,
            new ProductSearchFiltersDto(
                filters?.SubcategoryIds,
                filters?.BrandIds,
                filters?.Statuses,
                filters?.IsActiveBrand,
                filters?.HasBrand),
            request?.Sort?.Field,
            request?.Sort?.Direction,
            pagination.PageNumber,
            pagination.PageSize));

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

    [HttpPost("bulk")]
    public async Task<ActionResult<AdminMasterProductBulkOperationDto>> CreateProductsBulk([FromBody] BulkCreateMasterProductsRequest request)
    {
        var adminUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var command = new BulkCreateMasterProductsCommand(
            adminUserId,
            request.IdempotencyKey,
            request.Items.Select(item => new BulkCreateMasterProductItemInput(
                item.NameAr,
                item.NameEn,
                item.Slug,
                item.Barcode,
                item.CategoryId,
                item.BrandId,
                item.UnitId,
                item.Status,
                item.DescriptionAr,
                item.DescriptionEn)).ToList());

        var result = await Sender.Send(command);
        return AcceptedAtAction(nameof(GetBulkOperation), new { operationId = result.Id }, result);
    }

    [HttpGet("bulk/{operationId:guid}")]
    public async Task<ActionResult<AdminMasterProductBulkOperationDto>> GetBulkOperation(Guid operationId)
    {
        var adminUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetAdminMasterProductBulkOperationQuery(operationId, adminUserId));
        return Ok(result);
    }

    [HttpGet("bulk/{operationId:guid}/items")]
    public async Task<ActionResult<IReadOnlyList<AdminMasterProductBulkOperationItemDto>>> GetBulkOperationItems(Guid operationId)
    {
        var adminUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetAdminMasterProductBulkOperationItemsQuery(operationId, adminUserId));
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

public record BulkCreateMasterProductsRequest(
    string IdempotencyKey,
    IReadOnlyList<BulkCreateMasterProductItemRequest> Items);

public record BulkCreateMasterProductItemRequest(
    string NameAr,
    string NameEn,
    string? Slug,
    string? Barcode,
    Guid CategoryId,
    Guid? BrandId,
    Guid? UnitId,
    ProductStatus Status,
    string? DescriptionAr,
    string? DescriptionEn);

