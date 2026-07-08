using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;
using Zadana.Application.Modules.Catalog.Commands.Categories.DeleteCategory;
using Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategories;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryById;
using Zadana.Application.Modules.Catalog.Queries.Categories.SearchCategories;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/categories")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Tags("Catalog (Admins)")]
public class AdminCategoriesController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminCategoriesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetCategoriesQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<CatalogSearchResponse<CategoryDto, CategorySearchFiltersDto, CategorySearchFacetsDto>>> SearchCategories([FromBody] CategorySearchRequest? request)
    {
        var pagination = request?.Pagination ?? new CatalogPaginationRequest();
        var filters = request?.Filters;

        var result = await Sender.Send(new SearchCategoriesQuery(
            request?.Search,
            new CategorySearchFiltersDto(
                filters?.ParentCategoryId,
                filters?.Level,
                filters?.IsActive,
                filters?.HasChildren,
                filters?.CreatedAtFrom,
                filters?.CreatedAtTo),
            request?.Sort?.Field,
            request?.Sort?.Direction,
            pagination.PageNumber,
            pagination.PageSize));

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var command = new CreateCategoryCommand(
            request.NameAr,
            request.NameEn,
            request.ImageUrl,
            request.ParentCategoryId,
            request.DisplayOrder);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var command = new UpdateCategoryCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.ImageUrl,
            request.ParentCategoryId,
            request.DisplayOrder,
            request.IsActive);

        await Sender.Send(command);
        return Ok();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id)
    {
        var result = await Sender.Send(new GetCategoryByIdQuery(id));
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCategory(Guid id)
    {
        await Sender.Send(new DeleteCategoryCommand(id));
        return NoContent();
    }

    [HttpGet("deleted")]
    public async Task<ActionResult> GetDeletedCategories(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.IsDeleted)
            .OrderByDescending(c => c.DeletedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new { c.Id, c.NameAr, c.NameEn, c.ParentCategoryId, c.DeletedAtUtc })
            .ToListAsync(cancellationToken);

        return Ok(new { items, total, pageNumber, pageSize, hasMore = (pageNumber * pageSize) < total });
    }

    [HttpPatch("{id}/restore")]
    public async Task<ActionResult> RestoreCategory(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted, cancellationToken);

        if (category is null)
            return NotFound(new { error = "CATEGORY_NOT_FOUND_OR_NOT_DELETED" });

        category.Restore();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message_ar = "استعدنا التصنيف بنجاح", message_en = "Category restored successfully", id });
    }
}

