using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;

public class GetBrandProductsQueryHandler : IRequestHandler<GetBrandProductsQuery, BrandProductsDto>
{
    private const int DefaultPage = 1;
    private const int DefaultPerPage = 20;
    private const int MaxPerPage = 100;

    private readonly IApplicationDbContext _context;

    public GetBrandProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BrandProductsDto> Handle(GetBrandProductsQuery request, CancellationToken cancellationToken)
    {
        var brandExists = await _context.Brands
            .AsNoTracking()
            .AnyAsync(brand => brand.Id == request.BrandId && brand.IsActive, cancellationToken);

        if (!brandExists)
        {
            throw new NotFoundException(nameof(Brand), request.BrandId);
        }

        HashSet<Guid>? categoryScopeIds = null;
        if (request.CategoryId.HasValue && !request.SubcategoryId.HasValue)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .Select(category => new CategoryRow(category.Id, category.ParentCategoryId))
                .ToListAsync(cancellationToken);

            categoryScopeIds = BuildSubtree(request.CategoryId.Value, categories);
        }

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
                product.MasterProduct.BrandId == request.BrandId &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders &&
                (!request.SubcategoryId.HasValue || product.MasterProduct.CategoryId == request.SubcategoryId.Value) &&
                (!request.CategoryId.HasValue || request.SubcategoryId.HasValue || (categoryScopeIds != null && categoryScopeIds.Contains(product.MasterProduct.CategoryId))) &&
                (!request.UnitId.HasValue || product.MasterProduct.UnitOfMeasureId == request.UnitId.Value) &&
                (!request.MinPrice.HasValue || product.SellingPrice >= request.MinPrice.Value) &&
                (!request.MaxPrice.HasValue || product.SellingPrice <= request.MaxPrice.Value))
            .Select(product => new RawBrandProduct(
                product.Id,
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

                return new BrandProductSource(
                    product.Id,
                    product.CreatedAtUtc,
                    BrandCatalogQueryHelpers.PickLocalized(product.NameAr, product.NameEn),
                    BrandCatalogQueryHelpers.PickLocalized(product.StoreAr, product.StoreEn),
                    product.SellingPrice,
                    product.CompareAtPrice,
                    BrandCatalogQueryHelpers.PickLocalizedNullable(product.UnitAr, product.UnitEn),
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

        return new BrandProductsDto(
            new BrandProductsAppliedFiltersDto(
                request.CategoryId,
                request.SubcategoryId,
                request.UnitId,
                request.MinPrice,
                request.MaxPrice,
                BrandCatalogQueryHelpers.NormalizeSort(request.Sort)),
            total,
            page,
            perPage,
            items);
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

    private IEnumerable<BrandProductSource> ApplySorting(IEnumerable<BrandProductSource> products, string? sort)
    {
        return BrandCatalogQueryHelpers.NormalizeSort(sort) switch
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

    private BrandProductItemDto MapToProductItem(BrandProductSource product)
    {
        var isDiscounted = product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.SellingPrice;

        return new BrandProductItemDto(
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

    private static decimal CalculateDiscountRate(BrandProductSource product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= 0 || product.CompareAtPrice.Value <= product.SellingPrice)
        {
            return 0;
        }

        return (product.CompareAtPrice.Value - product.SellingPrice) / product.CompareAtPrice.Value;
    }

    private static string? FormatDiscount(BrandProductSource product)
    {
        var rate = CalculateDiscountRate(product);
        return rate <= 0
            ? null
            : $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
    }

    private static HashSet<Guid> BuildSubtree(Guid rootId, IReadOnlyCollection<CategoryRow> categories)
    {
        var activeChildrenByParent = categories
            .Where(child => child.ParentCategoryId.HasValue)
            .GroupBy(child => child.ParentCategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToArray());

        var result = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(rootId);

        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            if (!result.Add(currentId))
            {
                continue;
            }

            if (!activeChildrenByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                stack.Push(childId);
            }
        }

        return result;
    }

    private sealed record CategoryRow(Guid Id, Guid? ParentCategoryId);

    private sealed record VendorReviewStats(decimal AverageRating, int ReviewCount);

    private sealed record RawBrandProduct(
        Guid Id,
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

    private sealed record BrandProductSource(
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
