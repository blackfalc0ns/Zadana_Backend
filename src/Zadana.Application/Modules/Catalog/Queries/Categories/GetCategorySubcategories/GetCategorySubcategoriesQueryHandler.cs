using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategorySubcategories;

public class GetCategorySubcategoriesQueryHandler : IRequestHandler<GetCategorySubcategoriesQuery, List<CategoryListItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly CacheDurationSettings _durations;

    public GetCategorySubcategoriesQueryHandler(
        IApplicationDbContext context,
        IAppCache cache,
        IOptions<CachingSettings> cachingOptions)
    {
        _context = context;
        _cache = cache;
        _durations = cachingOptions.Value.Durations;
    }

    public async Task<List<CategoryListItemDto>> Handle(GetCategorySubcategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.CategorySubcategories(request.CategoryId),
            async token =>
            {
                if (request.CategoryId.HasValue)
                {
                    var categoryExists = await _context.Categories
                        .AsNoTracking()
                        .AnyAsync(c => c.Id == request.CategoryId.Value, token);

                    if (!categoryExists)
                    {
                        throw new NotFoundException(nameof(Category), request.CategoryId.Value);
                    }
                }

                var query = _context.Categories
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.ParentCategoryId != null);

                if (request.CategoryId.HasValue)
                {
                    query = query.Where(c => c.ParentCategoryId == request.CategoryId.Value);
                }
                else
                {
                    query = query.Where(c => !c.SubCategories.Any(child => child.IsActive));
                }

                var subcategories = await query
                    .Select(c => new RawCategoryListItem(
                        c.Id,
                        c.NameAr,
                        c.NameEn,
                        c.ImageUrl,
                        c.DisplayOrder))
                    .ToListAsync(token);

                return subcategories
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => PickLocalized(c.NameAr, c.NameEn), StringComparer.CurrentCultureIgnoreCase)
                    .Select(c => new CategoryListItemDto(
                        c.Id,
                        PickLocalized(c.NameAr, c.NameEn),
                        c.ImageUrl))
                    .ToList();
            },
            new AppCacheEntryOptions(_durations.PublicCatalogMetadata),
            [CacheTagNames.CatalogFilters],
            cancellationToken);
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim()
            ?? fallback?.Trim()
            ?? string.Empty;
    }

    private sealed record RawCategoryListItem(
        Guid Id,
        string? NameAr,
        string? NameEn,
        string? ImageUrl,
        int DisplayOrder);
}
