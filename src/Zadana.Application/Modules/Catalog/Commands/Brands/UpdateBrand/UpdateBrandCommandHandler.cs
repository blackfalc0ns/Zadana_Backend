using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;

public class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateBrandCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .Include(item => item.BrandCategories)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (brand == null)
            throw new NotFoundException(nameof(Brand), request.Id);

        var categoryIds = ResolveCategoryIds(request.CategoryId, request.CategoryIds);
        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId && item.ParentCategoryId != null, cancellationToken);

        if (category == null)
            throw new NotFoundException(nameof(Category), request.CategoryId);

        brand.Update(request.NameAr, request.NameEn, request.LogoUrl, request.CoverImageUrl, request.CategoryId);
        SyncBrandCategories(brand, categoryIds);

        if (request.IsActive && !brand.IsActive)
            brand.Activate();
        else if (!request.IsActive && brand.IsActive)
            brand.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }

    private void SyncBrandCategories(Brand brand, IReadOnlyList<Guid> categoryIds)
    {
        var desiredIds = categoryIds.ToHashSet();
        var existingLinks = brand.BrandCategories.ToList();

        foreach (var link in existingLinks.Where(link => !desiredIds.Contains(link.CategoryId)))
        {
            _context.BrandCategories.Remove(link);
        }

        var existingIds = existingLinks.Select(link => link.CategoryId).ToHashSet();
        foreach (var categoryId in desiredIds.Where(id => !existingIds.Contains(id)))
        {
            _context.BrandCategories.Add(new BrandCategory(brand.Id, categoryId));
        }
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
