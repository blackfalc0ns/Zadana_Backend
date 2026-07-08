using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Brands.BulkCreateBrands;
using Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;
using Zadana.Application.Modules.Catalog.Commands.Brands.DeleteBrand;
using Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;
using Zadana.Application.Modules.Catalog.Commands.BulkDeleteBrands;
using Zadana.Application.Modules.Catalog.Commands.BulkDeleteMasterProducts;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandBulkOperation;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandBulkOperationItems;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandById;
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
    private readonly IApplicationDbContext _context;

    public AdminBrandsController(
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> GetBrands([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetBrandsQuery(includeInactive));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BrandDto>> GetBrand(Guid id)
    {
        var result = await Sender.Send(new GetAdminBrandByIdQuery(id));
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
        var result = await Sender.Send(new CreateBrandCommand(
            request.NameAr,
            request.NameEn,
            request.LogoUrl,
            request.CoverImageUrl,
            request.CategoryId,
            request.CategoryIds));
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
                item.CoverImageUrl,
                item.CategoryId,
                item.CategoryIds,
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
            request.CoverImageUrl,
            request.CategoryId,
            request.CategoryIds,
            request.IsActive);

        await Sender.Send(command);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteBrand(Guid id)
    {
        await Sender.Send(new DeleteBrandCommand(id));
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    public async Task<ActionResult<BulkDeleteResult>> BulkDeleteBrands(
        [FromBody] BulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new BulkDeleteBrandsCommand(request.Ids), cancellationToken);
        return Ok(result);
    }

    [HttpGet("deleted")]
    public async Task<ActionResult> GetDeletedBrands(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Brands
            .IgnoreQueryFilters()
            .Where(b => b.IsDeleted)
            .OrderByDescending(b => b.DeletedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new { b.Id, b.NameAr, b.NameEn, b.LogoUrl, b.DeletedAtUtc })
            .ToListAsync(cancellationToken);

        return Ok(new { items, total, pageNumber, pageSize, hasMore = (pageNumber * pageSize) < total });
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<ActionResult> RestoreBrand(Guid id, CancellationToken cancellationToken = default)
    {
        var brand = await _context.Brands
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id && b.IsDeleted, cancellationToken);

        if (brand is null)
            return NotFound(new { error = "BRAND_NOT_FOUND_OR_NOT_DELETED" });

        brand.Restore();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message_ar = "استعدنا العلامة التجارية بنجاح", message_en = "Brand restored successfully", id });
    }
}

public record BulkCreateBrandsRequest(
    string IdempotencyKey,
    IReadOnlyList<BulkCreateBrandItemRequest> Items);

public record BulkCreateBrandItemRequest(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    string? CoverImageUrl,
    Guid? CategoryId,
    IReadOnlyList<Guid>? CategoryIds,
    bool IsActive);
