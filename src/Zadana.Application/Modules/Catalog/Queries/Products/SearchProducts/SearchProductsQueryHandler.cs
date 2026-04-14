using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResponseDto>
{
    private const int DefaultPage = 1;
    private const int DefaultPerPage = 20;
    private const int MaxPerPage = 100;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SearchProductsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SearchProductsResponseDto> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new SearchProductsResponseDto(string.Empty, 0, NormalizePage(request.Page), NormalizePerPage(request.PerPage), []);
        }

        var favoriteMasterProductIds = await LoadFavoriteMasterProductIdsAsync(cancellationToken);

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
                (!request.CategoryId.HasValue || product.MasterProduct.CategoryId == request.CategoryId.Value) &&
                (!request.BrandId.HasValue || product.MasterProduct.BrandId == request.BrandId.Value) &&
                (!request.MinPrice.HasValue || product.SellingPrice >= request.MinPrice.Value) &&
                (!request.MaxPrice.HasValue || product.SellingPrice <= request.MaxPrice.Value) &&
                (
                    product.MasterProduct.NameAr.Contains(normalizedQuery) ||
                    product.MasterProduct.NameEn.Contains(normalizedQuery) ||
                    (!string.IsNullOrWhiteSpace(product.CustomNameAr) && product.CustomNameAr.Contains(normalizedQuery)) ||
                    (!string.IsNullOrWhiteSpace(product.CustomNameEn) && product.CustomNameEn.Contains(normalizedQuery)) ||
                    (product.MasterProduct.DescriptionAr != null && product.MasterProduct.DescriptionAr.Contains(normalizedQuery)) ||
                    (product.MasterProduct.DescriptionEn != null && product.MasterProduct.DescriptionEn.Contains(normalizedQuery)) ||
                    (product.MasterProduct.Barcode != null && product.MasterProduct.Barcode.Contains(normalizedQuery))
                ))
            .Select(product => new RawSearchProduct(
                product.Id,
                product.MasterProductId,
                product.CreatedAtUtc,
                product.VendorId,
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

                return new SearchProductSource(
                    product.MasterProductId,
                    product.CreatedAtUtc,
                    PickLocalized(product.NameAr, product.NameEn),
                    PickLocalized(product.StoreAr, product.StoreEn),
                    product.SellingPrice,
                    product.CompareAtPrice,
                    PickLocalizedNullable(product.UnitAr, product.UnitEn),
                    product.ImageUrl,
                    salesCount,
                    reviewStats?.AverageRating,
                    reviewStats?.ReviewCount ?? 0);
            })
            .GroupBy(product => product.Id)
            .Select(group => group
                .OrderBy(product => product.SellingPrice)
                .ThenByDescending(product => product.CreatedAtUtc)
                .ThenBy(product => product.Store, StringComparer.CurrentCultureIgnoreCase)
                .First());

        var sortedProducts = ApplySorting(products, request.Sort).ToList();
        var total = sortedProducts.Count;
        var page = NormalizePage(request.Page);
        var perPage = NormalizePerPage(request.PerPage);

        var items = sortedProducts
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(product => MapToProductItem(product, favoriteMasterProductIds.Contains(product.Id)))
            .ToList();

        return new SearchProductsResponseDto(normalizedQuery, total, page, perPage, items);
    }

    private static int NormalizePage(int page) => page <= 0 ? DefaultPage : page;

    private static int NormalizePerPage(int perPage)
    {
        if (perPage <= 0)
        {
            return DefaultPerPage;
        }

        return Math.Min(perPage, MaxPerPage);
    }

    private IEnumerable<SearchProductSource> ApplySorting(IEnumerable<SearchProductSource> products, string? sort)
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

    private SearchProductItemDto MapToProductItem(SearchProductSource product, bool isFavorite)
    {
        var isDiscounted = product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.SellingPrice;

        return new SearchProductItemDto(
            product.Id,
            product.Name,
            product.Store,
            product.SellingPrice,
            isDiscounted ? product.CompareAtPrice : null,
            product.ImageUrl,
            product.Rating,
            product.ReviewCount,
            FormatDiscount(product),
            isFavorite,
            product.Unit,
            isDiscounted);
    }

    private async Task<HashSet<Guid>> LoadFavoriteMasterProductIdsAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue && string.IsNullOrWhiteSpace(_currentUserService.GuestDeviceId))
        {
            return [];
        }

        return await _context.CustomerFavorites
            .AsNoTracking()
            .Where(x =>
                (_currentUserService.UserId.HasValue && x.UserId == _currentUserService.UserId.Value) ||
                (!_currentUserService.UserId.HasValue && x.GuestId == _currentUserService.GuestDeviceId))
            .Select(x => x.MasterProductId)
            .ToHashSetAsync(cancellationToken);
    }

    private static decimal CalculateDiscountRate(SearchProductSource product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= 0 || product.CompareAtPrice.Value <= product.SellingPrice)
        {
            return 0;
        }

        return (product.CompareAtPrice.Value - product.SellingPrice) / product.CompareAtPrice.Value;
    }

    private static string? FormatDiscount(SearchProductSource product)
    {
        var rate = CalculateDiscountRate(product);
        return rate <= 0
            ? null
            : $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
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

    private static string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record VendorReviewStats(decimal AverageRating, int ReviewCount);

    private sealed record RawSearchProduct(
        Guid Id,
        Guid MasterProductId,
        DateTime CreatedAtUtc,
        Guid VendorId,
        string? NameAr,
        string? NameEn,
        string StoreAr,
        string StoreEn,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? UnitAr,
        string? UnitEn,
        string? ImageUrl);

    private sealed record SearchProductSource(
        Guid Id,
        DateTime CreatedAtUtc,
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
