using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Brands.BulkCreateBrands;
using Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;
using Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandBulkOperation;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandBulkOperationItems;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrands;
using Zadana.Application.Modules.Catalog.Queries.Brands.SearchBrands;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/brands")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Tags("Catalog (Admins)")]
public class AdminBrandsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public AdminBrandsController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> GetBrands([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetBrandsQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<CatalogSearchResponse<BrandDto, BrandSearchFiltersDto, BrandSearchFacetsDto>>> SearchBrands([FromBody] BrandSearchRequest? request)
    {
        var pagination = request?.Pagination ?? new CatalogPaginationRequest();
        var filters = request?.Filters;

        var result = await Sender.Send(new SearchBrandsQuery(
            request?.Search,
            new BrandSearchFiltersDto(
                filters?.CategoryId,
                filters?.IsActive,
                filters?.HasProducts,
                filters?.CreatedAtFrom,
                filters?.CreatedAtTo),
            request?.Sort?.Field,
            request?.Sort?.Direction,
            pagination.PageNumber,
            pagination.PageSize));

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<BrandDto>> CreateBrand([FromBody] CreateBrandRequest request)
    {
        var result = await Sender.Send(new CreateBrandCommand(request.NameAr, request.NameEn, request.LogoUrl, request.CategoryId));
        return Ok(result);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<AdminBrandBulkOperationDto>> CreateBrandsBulk([FromBody] BulkCreateBrandsRequest request)
    {
        var adminUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var command = new BulkCreateBrandsCommand(
            adminUserId,
            request.IdempotencyKey,
            request.Items.Select(item => new BulkCreateBrandItemInput(
                item.NameAr,
                item.NameEn,
                item.LogoUrl,
                item.CategoryId,
                item.IsActive)).ToList());

        var result = await Sender.Send(command);
        return AcceptedAtAction(nameof(GetBulkOperation), new { operationId = result.Id }, result);
    }

    [HttpGet("bulk/{operationId:guid}")]
    public async Task<ActionResult<AdminBrandBulkOperationDto>> GetBulkOperation(Guid operationId)
    {
        var adminUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetAdminBrandBulkOperationQuery(operationId, adminUserId));
        return Ok(result);
    }

    [HttpGet("bulk/{operationId:guid}/items")]
    public async Task<ActionResult<IReadOnlyList<AdminBrandBulkOperationItemDto>>> GetBulkOperationItems(Guid operationId)
    {
        var adminUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetAdminBrandBulkOperationItemsQuery(operationId, adminUserId));
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateBrand(Guid id, [FromBody] UpdateBrandRequest request)
    {
        var command = new UpdateBrandCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.LogoUrl,
            request.CategoryId,
            request.IsActive);

        await Sender.Send(command);
        return Ok();
    }
}

public record BulkCreateBrandsRequest(
    string IdempotencyKey,
    IReadOnlyList<BulkCreateBrandItemRequest> Items);

public record BulkCreateBrandItemRequest(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    Guid CategoryId,
    bool IsActive);
