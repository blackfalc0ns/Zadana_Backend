using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;

public class GetCategoryProductsQueryHandler : IRequestHandler<GetCategoryProductsQuery, CategoryProductsDto>
{
    private const int DefaultPage = 1;
    private const int DefaultPerPage = 20;
    private const int MaxPerPage = 100;

    private readonly IApplicationDbContext _context;

    public GetCategoryProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryProductsDto> Handle(GetCategoryProductsQuery request, CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Select(category => new CategoryScopeRow(
                category.Id,
                category.ParentCategoryId,
                category.NameAr,
                category.NameEn,
                category.DisplayOrder,
                category.IsActive))
            .ToListAsync(cancellationToken);

        var categoryScope = ResolveScope(request.CategoryId, categories)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);
        var categoryScopeIds = categoryScope.ActiveSubtreeIds.ToHashSet();

        var salesByVendorProductId = await _context.OrderItems
            .AsNoTracking()
            .Where(item => item.Order.Status == OrderStatus.Delivered)
            .GroupBy(item => item.VendorProductId)
            .Select(group => new
            {
                VendorProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(item => item.VendorProductId, item => item.Quantity, cancellationToken);

        var reviewStatsByVendorId = await _context.Reviews
            .AsNoTracking()
            .GroupBy(review => review.VendorId)
            .Select(group => new
            {
                VendorId = group.Key,
                AverageRating = Math.Round(group.Average(review => review.Rating), 1),
                ReviewCount = group.Count()
            })
            .ToDictionaryAsync(
                item => item.VendorId,
                item => new VendorReviewStats((decimal)item.AverageRating, item.ReviewCount),
                cancellationToken);

        var rawProducts = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders &&
                categoryScopeIds.Contains(product.MasterProduct.CategoryId) &&
                (!request.ProductTypeId.HasValue || product.MasterProduct.ProductTypeId == request.ProductTypeId.Value) &&
                (!request.PartId.HasValue || product.MasterProduct.PartId == request.PartId.Value) &&
                (!request.BrandId.HasValue || product.MasterProduct.BrandId == request.BrandId.Value) &&
                (!request.QuantityId.HasValue || product.MasterProduct.UnitOfMeasureId == request.QuantityId.Value) &&
                (!request.MinPrice.HasValue || product.SellingPrice >= request.MinPrice.Value) &&
                (!request.MaxPrice.HasValue || product.SellingPrice <= request.MaxPrice.Value))
            .Select(product => new RawCategoryProduct(
                product.Id,
                product.CreatedAtUtc,
                product.VendorId,
                product.MasterProduct.CategoryId,
                !string.IsNullOrWhiteSpace(product.CustomNameAr) ? product.CustomNameAr : product.MasterProduct.NameAr,
                !string.IsNullOrWhiteSpace(product.CustomNameEn) ? product.CustomNameEn : product.MasterProduct.NameEn,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.SellingPrice,
                product.CompareAtPrice,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                product.MasterProduct.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var products = rawProducts
            .Select(product =>
            {
                salesByVendorProductId.TryGetValue(product.Id, out var salesCount);
                reviewStatsByVendorId.TryGetValue(product.VendorId, out var reviewStats);

                return new CategoryProductSource(
                    product.Id,
                    product.CreatedAtUtc,
                    product.CategoryId,
                    PickLocalized(product.NameAr, product.NameEn),
                    PickLocalized(product.StoreAr, product.StoreEn),
                    product.SellingPrice,
                    product.CompareAtPrice,
                    PickLocalizedNullable(product.UnitAr, product.UnitEn),
                    product.ImageUrl,
                    salesCount,
                    reviewStats?.AverageRating,
                    reviewStats?.ReviewCount ?? 0);
            });

        var sortedProducts = ApplySorting(products, request.Sort).ToList();

        var total = sortedProducts.Count;
        var page = NormalizePage(request.Page);
        var perPage = NormalizePerPage(request.PerPage);
        var items = sortedProducts
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(MapToProductItem)
            .ToList();

        return new CategoryProductsDto(
            new CategoryProductsAppliedFiltersDto(
                request.ProductTypeId,
                request.PartId,
                request.QuantityId,
                request.BrandId,
                request.MinPrice,
                request.MaxPrice,
                NormalizeSort(request.Sort)),
            total,
            page,
            perPage,
            items);
    }

    private CategoryProductsDto CreateEmptyResult(CategoryScope scope, GetCategoryProductsQuery request) =>
        new(
            new CategoryProductsAppliedFiltersDto(
                request.ProductTypeId,
                request.PartId,
                request.QuantityId,
                request.BrandId,
                request.MinPrice,
                request.MaxPrice,
                NormalizeSort(request.Sort)),
            0,
            NormalizePage(request.Page),
            NormalizePerPage(request.PerPage),
            Array.Empty<CategoryProductItemDto>());

    private static int NormalizePage(int page) => page <= 0 ? DefaultPage : page;

    private static int NormalizePerPage(int perPage)
    {
        if (perPage <= 0)
        {
            return DefaultPerPage;
        }

        return Math.Min(perPage, MaxPerPage);
    }

    private IEnumerable<CategoryProductSource> ApplySorting(IEnumerable<CategoryProductSource> products, string? sort)
    {
        return NormalizeSort(sort) switch
        {
            "price_low_high" => products.OrderBy(product => product.SellingPrice)
                .ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase),
            "price_high_low" => products.OrderByDescending(product => product.SellingPrice)
                .ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase),
            "best_selling" => products.OrderByDescending(product => product.SalesCount)
                .ThenByDescending(product => product.Rating ?? 0)
                .ThenByDescending(product => product.CreatedAtUtc),
            "highest_rated" => products.OrderByDescending(product => product.Rating ?? 0)
                .ThenByDescending(product => product.ReviewCount)
                .ThenByDescending(product => product.SalesCount)
                .ThenByDescending(product => product.CreatedAtUtc),
            "alphabetical" => products.OrderBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(product => product.CreatedAtUtc),
            _ => products.OrderByDescending(product => product.CreatedAtUtc)
                .ThenByDescending(product => product.SalesCount)
        };
    }

    private static string? NormalizeSort(string? sort)
    {
        var normalized = sort?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "newest" => "newest",
            "price_low_high" => "price_low_high",
            "price_high_low" => "price_high_low",
            "best_selling" => "best_selling",
            "highest_rated" => "highest_rated",
            "alphabetical" => "alphabetical",
            _ => null
        };
    }

    private CategoryProductItemDto MapToProductItem(CategoryProductSource product)
    {
        var isDiscounted = product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.SellingPrice;

        return new CategoryProductItemDto(
            product.Id,
            product.Name,
            product.Store,
            product.SellingPrice,
            isDiscounted ? product.CompareAtPrice : null,
            product.ImageUrl,
            product.Rating,
            product.ReviewCount,
            FormatDiscount(product),
            false,
            product.Unit,
            isDiscounted);
    }

    private static decimal CalculateDiscountRate(CategoryProductSource product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= 0 || product.CompareAtPrice.Value <= product.SellingPrice)
        {
            return 0;
        }

        return (product.CompareAtPrice.Value - product.SellingPrice) / product.CompareAtPrice.Value;
    }

    private static string? FormatDiscount(CategoryProductSource product)
    {
        var rate = CalculateDiscountRate(product);
        return rate <= 0
            ? null
            : $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim()
            ?? fallback?.Trim()
            ?? string.Empty;
    }

    private string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static CategoryScope? ResolveScope(Guid categoryId, IReadOnlyCollection<CategoryScopeRow> categories)
    {
        var categoriesById = categories.ToDictionary(category => category.Id);
        if (!categoriesById.TryGetValue(categoryId, out var category) || !category.IsActive)
        {
            return null;
        }

        var activeChildrenByParent = categories
            .Where(child => child.IsActive && child.ParentCategoryId.HasValue)
            .GroupBy(child => child.ParentCategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var activeSubtreeIds = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(category.Id);

        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            activeSubtreeIds.Add(currentId);

            if (!activeChildrenByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                stack.Push(child.Id);
            }
        }

        return new CategoryScope(category, activeSubtreeIds);
    }

    private sealed record CategoryScope(
        CategoryScopeRow Category,
        IReadOnlyList<Guid> ActiveSubtreeIds);

    private sealed record CategoryScopeRow(
        Guid Id,
        Guid? ParentCategoryId,
        string? NameAr,
        string? NameEn,
        int DisplayOrder,
        bool IsActive);

    private sealed record VendorReviewStats(decimal AverageRating, int ReviewCount);

    private sealed record RawCategoryProduct(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid VendorId,
        Guid CategoryId,
        string? NameAr,
        string? NameEn,
        string StoreAr,
        string StoreEn,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? UnitAr,
        string? UnitEn,
        string? ImageUrl);

    private sealed record CategoryProductSource(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid CategoryId,
        string Name,
        string Store,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? Unit,
        string? ImageUrl,
        int SalesCount,
        decimal? Rating,
        int ReviewCount);
}
