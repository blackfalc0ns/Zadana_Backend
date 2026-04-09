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

namespace Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;

public class GetProductDetailsQueryHandler : IRequestHandler<GetProductDetailsQuery, ProductDetailsDto>
{
    private const int SimilarProductsLimit = 10;

    private readonly IApplicationDbContext _context;

    public GetProductDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDetailsDto> Handle(GetProductDetailsQuery request, CancellationToken cancellationToken)
    {
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

        var visibleOffers = await LoadVisibleOffersAsync(cancellationToken);

        var directOffer = visibleOffers.FirstOrDefault(offer => offer.VendorProductId == request.ProductId);
        Guid masterProductId;

        if (directOffer is not null)
        {
            masterProductId = directOffer.MasterProductId;
        }
        else
        {
            masterProductId = request.ProductId;
            if (!visibleOffers.Any(offer => offer.MasterProductId == masterProductId))
            {
                throw new NotFoundException(nameof(MasterProduct), request.ProductId);
            }
        }

        var offersForProduct = visibleOffers
            .Where(offer => offer.MasterProductId == masterProductId)
            .OrderBy(offer => offer.Price)
            .ThenByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.Store, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var defaultOffer = directOffer ?? offersForProduct.First();

        var galleryImages = offersForProduct
            .SelectMany(offer => offer.Images)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (galleryImages.Count == 0 && !string.IsNullOrWhiteSpace(defaultOffer.ImageUrl))
        {
            galleryImages.Add(defaultOffer.ImageUrl);
        }

        var vendorPrices = offersForProduct
            .Select(offer => new ProductDetailsVendorPriceDto(
                offer.VendorProductId,
                offer.Store,
                offer.StoreLogoUrl,
                offer.Price,
                offer.IsDiscounted ? offer.OldPrice : null,
                offer.IsDiscounted))
            .ToList();

        var similarProducts = visibleOffers
            .Where(offer => offer.CategoryId == defaultOffer.CategoryId && offer.MasterProductId != masterProductId)
            .GroupBy(offer => offer.MasterProductId)
            .Select(group => group
                .OrderBy(offer => offer.Price)
                .ThenByDescending(offer => offer.CreatedAtUtc)
                .First())
            .OrderByDescending(offer => salesByVendorProductId.GetValueOrDefault(offer.VendorProductId))
            .ThenByDescending(offer => reviewStatsByVendorId.TryGetValue(offer.VendorId, out var stats) ? stats.AverageRating : 0)
            .ThenByDescending(offer => offer.CreatedAtUtc)
            .Take(SimilarProductsLimit)
            .Select(offer =>
            {
                reviewStatsByVendorId.TryGetValue(offer.VendorId, out var stats);

                return new ProductDetailsSimilarProductDto(
                    offer.MasterProductId,
                    offer.Name,
                    offer.Store,
                    offer.Price,
                    offer.IsDiscounted ? offer.OldPrice : null,
                    offer.ImageUrl,
                    stats?.AverageRating,
                    stats?.ReviewCount ?? 0,
                    FormatDiscount(offer.Price, offer.OldPrice),
                    false,
                    offer.Unit,
                    offer.IsDiscounted);
            })
            .ToList();

        reviewStatsByVendorId.TryGetValue(defaultOffer.VendorId, out var defaultReviewStats);

        return new ProductDetailsDto(
            masterProductId,
            masterProductId,
            defaultOffer.VendorProductId,
            defaultOffer.Name,
            defaultOffer.Store,
            defaultOffer.Price,
            defaultOffer.IsDiscounted ? defaultOffer.OldPrice : null,
            defaultOffer.ImageUrl,
            galleryImages,
            defaultReviewStats?.AverageRating,
            defaultReviewStats?.ReviewCount ?? 0,
            FormatDiscount(defaultOffer.Price, defaultOffer.OldPrice),
            false,
            defaultOffer.Unit,
            defaultOffer.IsDiscounted,
            defaultOffer.Description,
            vendorPrices,
            similarProducts);
    }

    private async Task<List<VisibleOfferRow>> LoadVisibleOffersAsync(CancellationToken cancellationToken)
    {
        var offers = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders)
            .Select(product => new RawVisibleOfferRow(
                product.Id,
                product.MasterProductId,
                product.VendorId,
                product.MasterProduct.CategoryId,
                product.CreatedAtUtc,
                !string.IsNullOrWhiteSpace(product.CustomNameAr) ? product.CustomNameAr : product.MasterProduct.NameAr,
                !string.IsNullOrWhiteSpace(product.CustomNameEn) ? product.CustomNameEn : product.MasterProduct.NameEn,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.Vendor.LogoUrl,
                product.SellingPrice,
                product.CompareAtPrice,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                product.MasterProduct.DescriptionAr,
                product.MasterProduct.DescriptionEn,
                product.MasterProduct.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return rawOffersToVisibleOffers(raw: offers);
    }

    private List<VisibleOfferRow> rawOffersToVisibleOffers(List<RawVisibleOfferRow> raw)
    {
        return raw.Select(offer => new VisibleOfferRow(
            offer.VendorProductId,
            offer.MasterProductId,
            offer.VendorId,
            offer.CategoryId,
            offer.CreatedAtUtc,
            PickLocalized(offer.NameAr, offer.NameEn),
            PickLocalized(offer.StoreAr, offer.StoreEn),
            offer.StoreLogoUrl,
            offer.SellingPrice,
            offer.CompareAtPrice,
            PickLocalizedNullable(offer.UnitAr, offer.UnitEn),
            PickLocalizedNullable(offer.DescriptionAr, offer.DescriptionEn),
            offer.Images.Where(url => !string.IsNullOrWhiteSpace(url)).ToList()))
        .ToList();
    }

    private static string? FormatDiscount(decimal price, decimal? oldPrice)
    {
        if (!oldPrice.HasValue || oldPrice.Value <= 0 || oldPrice.Value <= price)
        {
            return null;
        }

        var rate = (oldPrice.Value - price) / oldPrice.Value;
        return $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
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

    private sealed record RawVisibleOfferRow(
        Guid VendorProductId,
        Guid MasterProductId,
        Guid VendorId,
        Guid CategoryId,
        DateTime CreatedAtUtc,
        string? NameAr,
        string? NameEn,
        string StoreAr,
        string StoreEn,
        string? StoreLogoUrl,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? UnitAr,
        string? UnitEn,
        string? DescriptionAr,
        string? DescriptionEn,
        List<string> Images);

    private sealed record VisibleOfferRow(
        Guid VendorProductId,
        Guid MasterProductId,
        Guid VendorId,
        Guid CategoryId,
        DateTime CreatedAtUtc,
        string Name,
        string Store,
        string? StoreLogoUrl,
        decimal Price,
        decimal? OldPrice,
        string? Unit,
        string? Description,
        List<string> Images)
    {
        public string? ImageUrl => Images.FirstOrDefault();
        public bool IsDiscounted => OldPrice.HasValue && OldPrice.Value > Price;
    }
}
