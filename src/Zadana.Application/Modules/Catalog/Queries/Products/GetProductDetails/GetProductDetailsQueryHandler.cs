using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Application.Modules.Catalog.Queries;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;

public class GetProductDetailsQueryHandler : IRequestHandler<GetProductDetailsQuery, ProductDetailsDto>
{
    private const int SimilarProductsLimit = 10;

    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly ICatalogReadCacheService _catalogReadCacheService;
    private readonly CacheDurationSettings _durations;

    public GetProductDetailsQueryHandler(
        IApplicationDbContext context,
        IAppCache cache,
        ICatalogReadCacheService catalogReadCacheService,
        IOptions<CachingSettings> cachingOptions)
    {
        _context = context;
        _cache = cache;
        _catalogReadCacheService = catalogReadCacheService;
        _durations = cachingOptions.Value.Durations;
    }

    public async Task<ProductDetailsDto> Handle(GetProductDetailsQuery request, CancellationToken cancellationToken)
    {
        var baseResponse = await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.ProductDetails(request.ProductId),
            token => BuildBaseResponseAsync(request, token),
            new AppCacheEntryOptions(_durations.BrowseBase),
            [CacheTagNames.Catalog],
            cancellationToken);

        var favoriteMasterProductIds = await _catalogReadCacheService.GetCurrentFavoriteMasterProductIdsAsync(cancellationToken);
        return CatalogQueryFavoriteOverlays.ApplyFavorites(baseResponse, favoriteMasterProductIds);
    }

    private async Task<ProductDetailsDto> BuildBaseResponseAsync(
        GetProductDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var salesByVendorProductId = await _catalogReadCacheService.GetDeliveredSalesByVendorProductIdAsync(cancellationToken);
        var reviewStatsByVendorId = await _catalogReadCacheService.GetVendorReviewStatsByVendorIdAsync(cancellationToken);
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
        var variantGroupId = defaultOffer.VariantGroupId;

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

        var variantGroupOffers = visibleOffers
            .Where(offer => offer.VariantGroupId == variantGroupId)
            .ToList();

        var variantOffersByMasterProduct = variantGroupOffers
            .GroupBy(offer => offer.MasterProductId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(offer => offer.Price)
                .ThenByDescending(offer => offer.CreatedAtUtc)
                .ThenBy(offer => offer.Store, StringComparer.CurrentCultureIgnoreCase)
                .ToList());

        var variantOptions = variantOffersByMasterProduct
            .Select(kvp =>
            {
                var cheapest = kvp.Value.First();
                var variantVendorPrices = kvp.Value
                    .Select(offer => new ProductDetailsVendorPriceDto(
                        offer.VendorProductId,
                        offer.Store,
                        offer.StoreLogoUrl,
                        offer.Price,
                        offer.IsDiscounted ? offer.OldPrice : null,
                        offer.IsDiscounted))
                    .ToList();

                return new ProductDetailsVariantOptionDto(
                    cheapest.MasterProductId,
                    cheapest.VendorProductId,
                    cheapest.NameAr ?? cheapest.Name,
                    cheapest.NameEn ?? cheapest.Name,
                    cheapest.DisplaySizeAr,
                    cheapest.DisplaySizeEn,
                    cheapest.MasterProductId == masterProductId,
                    cheapest.ImageUrl,
                    cheapest.Images,
                    cheapest.PackageTypeAr,
                    cheapest.PackageTypeEn,
                    cheapest.MeasurementValue,
                    cheapest.MeasurementUnitAr,
                    cheapest.MeasurementUnitEn,
                    cheapest.Unit,
                    cheapest.Price,
                    cheapest.IsDiscounted ? cheapest.OldPrice : null,
                    cheapest.IsDiscounted,
                    variantVendorPrices);
            })
            .OrderBy(option => option.MeasurementValue ?? decimal.MaxValue)
            .ThenBy(option => option.NameAr, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var similarOfferRows = visibleOffers
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
            .ToList();

        var similarProducts = similarOfferRows
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
            variantOptions,
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
                product.MasterProduct.VariantGroupId,
                product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameAr : null,
                product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameEn : null,
                product.MasterProduct.MeasurementValue,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameAr : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameEn : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.Symbol : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.Symbol : null,
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
            NormalizeText(offer.NameAr),
            NormalizeText(offer.NameEn),
            PickLocalized(offer.NameAr, offer.NameEn),
            NormalizeText(offer.StoreAr),
            NormalizeText(offer.StoreEn),
            PickLocalized(offer.StoreAr, offer.StoreEn),
            offer.StoreLogoUrl,
            offer.SellingPrice,
            offer.CompareAtPrice,
            offer.VariantGroupId,
            NormalizeText(offer.PackageTypeAr),
            NormalizeText(offer.PackageTypeEn),
            offer.MeasurementValue,
            NormalizeText(offer.MeasurementUnitAr),
            NormalizeText(offer.MeasurementUnitEn),
            NormalizeText(offer.MeasurementUnitSymbol),
            MasterProductDisplayDto.BuildDisplaySize(offer.PackageTypeAr, offer.MeasurementValue, offer.MeasurementUnitAr, offer.MeasurementUnitSymbol, true),
            MasterProductDisplayDto.BuildDisplaySize(offer.PackageTypeEn, offer.MeasurementValue, offer.MeasurementUnitEn, offer.MeasurementUnitSymbol, false),
            PickLocalizedNullable(
                MasterProductDisplayDto.BuildLegacyUnit(offer.PackageTypeAr, offer.MeasurementUnitAr, true),
                MasterProductDisplayDto.BuildLegacyUnit(offer.PackageTypeEn, offer.MeasurementUnitEn, false)),
            NormalizeText(offer.DescriptionAr),
            NormalizeText(offer.DescriptionEn),
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

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
        Guid VariantGroupId,
        string? PackageTypeAr,
        string? PackageTypeEn,
        decimal? MeasurementValue,
        string? MeasurementUnitAr,
        string? MeasurementUnitEn,
        string? MeasurementUnitSymbol,
        string? DescriptionAr,
        string? DescriptionEn,
        List<string> Images);

    private sealed record VisibleOfferRow(
        Guid VendorProductId,
        Guid MasterProductId,
        Guid VendorId,
        Guid CategoryId,
        DateTime CreatedAtUtc,
        string? NameAr,
        string? NameEn,
        string Name,
        string? StoreAr,
        string? StoreEn,
        string Store,
        string? StoreLogoUrl,
        decimal Price,
        decimal? OldPrice,
        Guid VariantGroupId,
        string? PackageTypeAr,
        string? PackageTypeEn,
        decimal? MeasurementValue,
        string? MeasurementUnitAr,
        string? MeasurementUnitEn,
        string? MeasurementUnitSymbol,
        string? DisplaySizeAr,
        string? DisplaySizeEn,
        string? Unit,
        string? DescriptionAr,
        string? DescriptionEn,
        string? Description,
        List<string> Images)
    {
        public string? ImageUrl => Images.FirstOrDefault();
        public bool IsDiscounted => OldPrice.HasValue && OldPrice.Value > Price;
    }
}
