using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Categories.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var allCategories = await query.ToListAsync(cancellationToken);
        var brandsCountByCategoryId = await _context.Brands
            .AsNoTracking()
            .Where(brand => brand.CategoryId.HasValue)
            .GroupBy(brand => brand.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

        // Build the tree in memory
        var rootCategories = allCategories
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapToDtoWithSubCategories(c, allCategories, brandsCountByCategoryId, 0))
            .ToList();

        return rootCategories;
    }

    private CategoryDto MapToDtoWithSubCategories(Category category, List<Category> allCategories, IReadOnlyDictionary<Guid, int> brandsCountByCategoryId, int level)
    {
        var subCategories = allCategories
            .Where(c => c.ParentCategoryId == category.Id)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapToDtoWithSubCategories(c, allCategories, brandsCountByCategoryId, level + 1))
            .ToList();
        var brandsCount = brandsCountByCategoryId.TryGetValue(category.Id, out var count) ? count : 0;

        return new CategoryDto(
            category.Id,
            category.NameAr,
            category.NameEn,
            category.ImageUrl,
            category.ParentCategoryId,
            category.DisplayOrder,
            category.IsActive,
            ParentNameAr: null,
            ParentNameEn: null,
            CreatedAtUtc: category.CreatedAtUtc,
            UpdatedAtUtc: category.UpdatedAtUtc,
            MasterProductsCount: 0,
            BrandsCount: brandsCount,
            Level: level,
            SubCategories: subCategories.Any() ? subCategories : null);
    }
}
