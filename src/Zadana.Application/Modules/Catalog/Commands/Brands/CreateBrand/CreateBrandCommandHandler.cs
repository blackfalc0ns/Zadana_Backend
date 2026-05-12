using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;

public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, BrandDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public CreateBrandCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<BrandDto> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var categoryIds = ResolveCategoryIds(request.CategoryId, request.CategoryIds);
        var category = await _context.Categories
            .AsNoTracking()
            .FirstAsync(item => item.Id == request.CategoryId && item.ParentCategoryId != null, cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(item => categoryIds.Contains(item.Id) && item.ParentCategoryId != null)
            .OrderBy(item => item.NameEn)
            .ToListAsync(cancellationToken);

        var brand = new Brand(request.NameAr, request.NameEn, request.LogoUrl, request.CoverImageUrl, request.CategoryId);

        _context.Brands.Add(brand);
        foreach (var categoryId in categoryIds)
        {
            _context.BrandCategories.Add(new BrandCategory(brand.Id, categoryId));
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        return new BrandDto(
            brand.Id,
            brand.NameAr,
            brand.NameEn,
            brand.LogoUrl,
            brand.CoverImageUrl,
            brand.CategoryId,
            categoryIds,
            categories.Select(item => new BrandCategoryLinkDto(item.Id, item.NameAr, item.NameEn)).ToList(),
            category.NameAr,
            category.NameEn,
            brand.IsActive,
            0,
            brand.CreatedAtUtc,
            brand.UpdatedAtUtc);
    }

    private static IReadOnlyList<Guid> ResolveCategoryIds(Guid categoryId, IReadOnlyList<Guid>? categoryIds)
    {
        var resolved = categoryIds is { Count: > 0 }
            ? categoryIds
            : [categoryId];

        return resolved
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
    }
}
