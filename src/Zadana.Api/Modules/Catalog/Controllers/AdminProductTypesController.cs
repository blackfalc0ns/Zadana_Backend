using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/product-types")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Tags("Catalog (Admins)")]
public class AdminProductTypesController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminProductTypesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult> GetProductTypes(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool includeInactive = false)
    {
        var query = _context.ProductTypes.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(pt => pt.IsActive);
        if (categoryId.HasValue)
            query = query.Where(pt => pt.CategoryId == categoryId.Value);

        var items = await query
            .OrderBy(pt => pt.NameEn)
            .Select(pt => new { pt.Id, pt.NameAr, pt.NameEn, pt.CategoryId, pt.IsActive, PartsCount = pt.Parts.Count })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetProductType(Guid id)
    {
        var pt = await _context.ProductTypes.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.NameAr, p.NameEn, p.CategoryId, p.IsActive,
                Parts = p.Parts.Select(part => new { part.Id, part.NameAr, part.NameEn, part.IsActive })
            })
            .FirstOrDefaultAsync();

        return pt is null ? NotFound() : Ok(pt);
    }

    [HttpPost]
    public async Task<ActionResult> CreateProductType([FromBody] ProductTypeRequest request)
    {
        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists) throw new NotFoundException("Category", request.CategoryId);

        var entity = new ProductType(request.NameAr, request.NameEn, request.CategoryId);
        _context.ProductTypes.Add(entity);
        await _context.SaveChangesAsync(default);
        return Ok(new { entity.Id, entity.NameAr, entity.NameEn, entity.CategoryId, entity.IsActive });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateProductType(Guid id, [FromBody] ProductTypeRequest request)
    {
        var entity = await _context.ProductTypes.FindAsync(id);
        if (entity is null) throw new NotFoundException("ProductType", id);

        entity.Update(request.NameAr, request.NameEn, request.CategoryId);
        if (request.IsActive && !entity.IsActive) entity.Activate();
        else if (!request.IsActive && entity.IsActive) entity.Deactivate();

        await _context.SaveChangesAsync(default);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteProductType(Guid id)
    {
        var entity = await _context.ProductTypes.FindAsync(id);
        if (entity is null) throw new NotFoundException("ProductType", id);

        if (await _context.MasterProducts.AnyAsync(p => p.ProductTypeId == id))
            throw new BusinessRuleException("PRODUCT_TYPE_HAS_PRODUCTS", "ما تقدر تحذف النوع لأنه مرتبط بمنتجات.");
        if (await _context.Parts.AnyAsync(p => p.ProductTypeId == id))
            throw new BusinessRuleException("PRODUCT_TYPE_HAS_PARTS", "ما تقدر تحذف النوع لأنه يحتوي على أجزاء.");

        _context.ProductTypes.Remove(entity);
        await _context.SaveChangesAsync(default);
        return NoContent();
    }

    // ── Parts ──

    [HttpGet("{productTypeId:guid}/parts")]
    public async Task<ActionResult> GetParts(Guid productTypeId, [FromQuery] bool includeInactive = false)
    {
        var query = _context.Parts.AsNoTracking().Where(p => p.ProductTypeId == productTypeId);
        if (!includeInactive) query = query.Where(p => p.IsActive);

        var items = await query.OrderBy(p => p.NameEn)
            .Select(p => new { p.Id, p.NameAr, p.NameEn, p.ProductTypeId, p.IsActive })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("{productTypeId:guid}/parts")]
    public async Task<ActionResult> CreatePart(Guid productTypeId, [FromBody] PartRequest request)
    {
        if (!await _context.ProductTypes.AnyAsync(pt => pt.Id == productTypeId))
            throw new NotFoundException("ProductType", productTypeId);

        var entity = new Part(request.NameAr, request.NameEn, productTypeId);
        _context.Parts.Add(entity);
        await _context.SaveChangesAsync(default);
        return Ok(new { entity.Id, entity.NameAr, entity.NameEn, entity.ProductTypeId, entity.IsActive });
    }

    [HttpPut("{productTypeId:guid}/parts/{partId:guid}")]
    public async Task<ActionResult> UpdatePart(Guid productTypeId, Guid partId, [FromBody] PartRequest request)
    {
        var entity = await _context.Parts.FirstOrDefaultAsync(p => p.Id == partId && p.ProductTypeId == productTypeId);
        if (entity is null) throw new NotFoundException("Part", partId);

        entity.Update(request.NameAr, request.NameEn, productTypeId);
        if (request.IsActive && !entity.IsActive) entity.Activate();
        else if (!request.IsActive && entity.IsActive) entity.Deactivate();

        await _context.SaveChangesAsync(default);
        return Ok();
    }

    [HttpDelete("{productTypeId:guid}/parts/{partId:guid}")]
    public async Task<ActionResult> DeletePart(Guid productTypeId, Guid partId)
    {
        var entity = await _context.Parts.FirstOrDefaultAsync(p => p.Id == partId && p.ProductTypeId == productTypeId);
        if (entity is null) throw new NotFoundException("Part", partId);

        if (await _context.MasterProducts.AnyAsync(p => p.PartId == partId))
            throw new BusinessRuleException("PART_HAS_PRODUCTS", "ما تقدر تحذف الجزء لأنه مرتبط بمنتجات.");

        _context.Parts.Remove(entity);
        await _context.SaveChangesAsync(default);
        return NoContent();
    }
}

public record ProductTypeRequest(string NameAr, string NameEn, Guid CategoryId, bool IsActive = true);
public record PartRequest(string NameAr, string NameEn, bool IsActive = true);
