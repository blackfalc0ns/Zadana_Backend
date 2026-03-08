using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryById;

public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, CategoryDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCategoryByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryDto?> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .Include(c => c.SubCategories)
            .Include(c => c.ParentCategory)
            .Include(c => c.MasterProducts)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null) return null;

        var level = await CalculateLevelAsync(category.Id, cancellationToken);

        return new CategoryDto(
            category.Id,
            category.NameAr,
            category.NameEn,
            category.ImageUrl,
            category.ParentCategoryId,
            category.DisplayOrder,
            category.IsActive,
            category.ParentCategory?.NameAr,
            category.ParentCategory?.NameEn,
            category.CreatedAtUtc,
            category.UpdatedAtUtc,
            category.MasterProducts?.Count ?? 0,
            Level: level,
            SubCategories: category.SubCategories?
                .OrderBy(sc => sc.DisplayOrder)
                .Select(sc => new CategoryDto(
                    sc.Id,
                    sc.NameAr,
                    sc.NameEn,
                    sc.ImageUrl,
                    sc.ParentCategoryId,
                    sc.DisplayOrder,
                    sc.IsActive,
                    null,
                    null,
                    null,
                    null,
                    0,
                    Level: level + 1,
                    null))
                .ToList());
    }

    private async Task<int> CalculateLevelAsync(Guid id, CancellationToken ct)
    {
        int level = 0;
        var currentId = (Guid?)id;
        while (currentId != null)
        {
            var parentId = await _context.Categories
                .Where(c => c.Id == currentId)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync(ct);

            if (parentId == null) break;

            level++;
            currentId = parentId;
        }
        return level;
    }
}
