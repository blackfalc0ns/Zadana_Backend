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

        // Build the tree in memory
        var rootCategories = allCategories
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapToDtoWithSubCategories(c, allCategories))
            .ToList();

        return rootCategories;
    }

    private CategoryDto MapToDtoWithSubCategories(Category category, List<Category> allCategories)
    {
        var subCategories = allCategories
            .Where(c => c.ParentCategoryId == category.Id)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapToDtoWithSubCategories(c, allCategories))
            .ToList();

        return new CategoryDto(
            category.Id,
            category.NameAr,
            category.NameEn,
            category.ParentCategoryId,
            category.DisplayOrder,
            category.IsActive,
            subCategories.Any() ? subCategories : null);
    }
}
