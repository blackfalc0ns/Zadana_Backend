using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategorySubcategories;

public class GetCategorySubcategoriesQueryHandler : IRequestHandler<GetCategorySubcategoriesQuery, List<CategoryListItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategorySubcategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryListItemDto>> Handle(GetCategorySubcategoriesQuery request, CancellationToken cancellationToken)
    {
        var categoryExists = await _context.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new NotFoundException(nameof(Category), request.CategoryId);
        }

        var subcategories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.ParentCategoryId == request.CategoryId && c.IsActive)
            .Select(c => new RawCategoryListItem(
                c.Id,
                c.NameAr,
                c.NameEn,
                c.ImageUrl,
                c.DisplayOrder))
            .ToListAsync(cancellationToken);

        return subcategories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => PickLocalized(c.NameAr, c.NameEn), StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new CategoryListItemDto(
                c.Id,
                PickLocalized(c.NameAr, c.NameEn),
                c.ImageUrl))
            .ToList();
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
