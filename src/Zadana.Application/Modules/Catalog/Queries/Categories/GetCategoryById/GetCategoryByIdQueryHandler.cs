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
        var directCategoryIds = category.SubCategories.Select(item => item.Id).Append(category.Id).ToList();
        var brandsCountByCategoryId = await _context.Brands
            .AsNoTracking()
            .Where(brand => brand.CategoryId.HasValue && directCategoryIds.Contains(brand.CategoryId.Value))
            .GroupBy(brand => brand.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

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
            brandsCountByCategoryId.TryGetValue(category.Id, out var categoryCount) ? categoryCount : 0,
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
                    brandsCountByCategoryId.TryGetValue(sc.Id, out var childCount) ? childCount : 0,
                    Level: level + 1,
                    SubCategories: null))
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
