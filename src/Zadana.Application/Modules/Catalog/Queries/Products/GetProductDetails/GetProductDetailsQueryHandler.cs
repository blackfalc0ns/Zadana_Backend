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
using Zadana.Application.Modules.Vendors.Support;
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
        var offers = await LoadCandidateOffersAsync(cancellationToken);
        var visibleOffers = offers.Where(offer => offer.IsVisibleInCatalog).ToList();

        var directOffer = offers.FirstOrDefault(offer => offer.VendorProductId == request.ProductId);
        Guid masterProductId;

        if (directOffer is not null)
        {
            masterProductId = directOffer.MasterProductId;
        }
        else
        {
            masterProductId = request.ProductId;
            var masterProductExists = await _context.MasterProducts
                .AsNoTracking()
                .AnyAsync(
                    product => product.Id == masterProductId && !product.IsDeleted && product.Status != ProductStatus.Discontinued,
                    cancellationToken);

            if (!masterProductExists && !offers.Any(offer => offer.MasterProductId == masterProductId))
            {
                throw new NotFoundException(nameof(MasterProduct), request.ProductId);
            }
        }

        var visibleOffersForProduct = visibleOffers
            .Where(offer => offer.MasterProductId == masterProductId)
            .OrderBy(offer => offer.Price)
            .ThenByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.Store, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var allOffersForProduct = offers
            .Where(offer => offer.MasterProductId == masterProductId)
            .OrderBy(offer => offer.Price)
            .ThenByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.Store, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var defaultOffer = directOffer ?? visibleOffersForProduct.FirstOrDefault() ?? allOffersForProduct.FirstOrDefault();
        var variantGroupId = defaultOffer?.VariantGroupId
            ?? await _context.MasterProducts
                .AsNoTracking()
                .Where(product => product.Id == masterProductId && !product.IsDeleted)
                .Select(product => product.VariantGroupId)
                .FirstOrDefaultAsync(cancellationToken);
        var variantGroupMembers = await LoadVariantGroupMembersAsync(variantGroupId, masterProductId, cancellationToken);
        var currentVariantMember = variantGroupMembers.First(member => member.Id == masterProductId);
        var variantGroupKey = GetProductGroupKey(variantGroupId, masterProductId);
        var isExplicitlyOfflineSelection = directOffer is not null && !directOffer.IsVisibleInCatalog;
        var sourceOffersForProduct = visibleOffersForProduct.Count > 0 ? visibleOffersForProduct : allOffersForProduct;

        var galleryImages = sourceOffersForProduct
            .SelectMany(offer => offer.Images)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (galleryImages.Count == 0 && !string.IsNullOrWhiteSpace(defaultOffer?.ImageUrl))
        {
            galleryImages.Add(defaultOffer.ImageUrl);
        }
        else if (galleryImages.Count == 0 && !string.IsNullOrWhiteSpace(currentVariantMember.PrimaryImageUrl))
        {
            galleryImages.Add(currentVariantMember.PrimaryImageUrl);
        }

        var vendorPrices = isExplicitlyOfflineSelection
            ? []
            : visibleOffersForProduct
                .Select(offer => new ProductDetailsVendorPriceDto(
                    offer.VendorProductId,
                    offer.Store,
                    offer.StoreLogoUrl,
                    offer.Price,
                    offer.IsDiscounted ? offer.OldPrice : null,
                    offer.IsDiscounted))
                .ToList();

        var variantGroupOffers = offers
            .Where(offer => GetProductGroupKey(offer.VariantGroupId, offer.MasterProductId) == variantGroupKey)
            .ToList();

        var variantOffersByMasterProduct = variantGroupOffers
            .GroupBy(offer => offer.MasterProductId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(offer => offer.Price)
                .ThenByDescending(offer => offer.CreatedAtUtc)
                .ThenBy(offer => offer.Store, StringComparer.CurrentCultureIgnoreCase)
                .ToList());

        var currentVariantAvailability = ResolveVariantAvailability(
            variantGroupMembers.First(member => member.Id == masterProductId),
            variantOffersByMasterProduct.GetValueOrDefault(masterProductId, []));

        var isAvailableForPurchase = !isExplicitlyOfflineSelection && currentVariantAvailability.IsAvailableForPurchase;
        var isOnlineNow = isExplicitlyOfflineSelection
            ? directOffer!.IsOnlineNow
            : currentVariantAvailability.IsOnlineNow;
        var unavailableReason = isAvailableForPurchase
            ? null
            : isExplicitlyOfflineSelection
                ? directOffer!.UnavailableReason
                : currentVariantAvailability.UnavailableReason;

        var variantOptions = variantGroupMembers
            .Select(member =>
            {
                var memberOffers = variantOffersByMasterProduct.GetValueOrDefault(member.Id, []);
                var memberAvailability = ResolveVariantAvailability(member, memberOffers);
                var representativeOffer = memberOffers.FirstOrDefault();
                var visibleMemberOffers = memberOffers.Where(offer => offer.IsVisibleInCatalog).ToList();
                var pricedOffer = visibleMemberOffers.FirstOrDefault() ?? representativeOffer;

                var variantVendorPrices = visibleMemberOffers
                    .Select(offer => new ProductDetailsVendorPriceDto(
                        offer.VendorProductId,
                        offer.Store,
                        offer.StoreLogoUrl,
                        offer.Price,
                        offer.IsDiscounted ? offer.OldPrice : null,
                        offer.IsDiscounted))
                    .ToList();

                var images = memberOffers
                    .SelectMany(offer => offer.Images)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (images.Count == 0 && !string.IsNullOrWhiteSpace(member.PrimaryImageUrl))
                {
                    images.Add(member.PrimaryImageUrl);
                }

                return new ProductDetailsVariantOptionDto(
                    member.Id,
                    memberAvailability.IsAvailableForPurchase ? pricedOffer?.VendorProductId : null,
                    member.NameAr ?? member.NameEn,
                    member.NameEn ?? member.NameAr,
                    member.DisplaySizeAr,
                    member.DisplaySizeEn,
                    member.Id == masterProductId,
                    images.FirstOrDefault(),
                    images,
                    member.PackageTypeAr,
                    member.PackageTypeEn,
                    member.MeasurementValue,
                    member.MeasurementUnitAr,
                    member.MeasurementUnitEn,
                    member.Unit,
                    memberAvailability.IsAvailableForPurchase ? pricedOffer?.Price : null,
                    memberAvailability.IsAvailableForPurchase && pricedOffer is { IsDiscounted: true }
                        ? pricedOffer.OldPrice
                        : null,
                    memberAvailability.IsAvailableForPurchase && pricedOffer is { IsDiscounted: true },
                    memberAvailability.IsOnlineNow,
                    memberAvailability.IsAvailableForPurchase,
                    memberAvailability.UnavailableReason,
                    variantVendorPrices);
            })
            .OrderBy(option => option.MeasurementValue ?? decimal.MaxValue)
            .ThenBy(option => option.NameAr, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var categoryId = defaultOffer?.CategoryId
            ?? await _context.MasterProducts
                .AsNoTracking()
                .Where(product => product.Id == masterProductId && !product.IsDeleted)
                .Select(product => product.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);

        var similarOfferRows = visibleOffers
            .Where(offer => offer.CategoryId == categoryId)
            .Where(offer => GetProductGroupKey(offer.VariantGroupId, offer.MasterProductId) != variantGroupKey)
            .GroupBy(offer => GetProductGroupKey(offer.VariantGroupId, offer.MasterProductId))
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
                    offer.IsDiscounted,
                    offer.ShowPriceOnCard);
            })
            .ToList();

        var displayOffer = defaultOffer ?? variantOffersByMasterProduct.GetValueOrDefault(masterProductId, []).FirstOrDefault();
        var displayName = displayOffer?.Name
            ?? PickLocalizedNullable(currentVariantMember.NameAr, currentVariantMember.NameEn)
            ?? string.Empty;
        var displayStore = displayOffer?.Store ?? string.Empty;
        var displayPrice = displayOffer?.Price ?? 0m;
        var displayOldPrice = displayOffer is { IsDiscounted: true } ? displayOffer.OldPrice : null;
        var displayImageUrl = displayOffer?.ImageUrl ?? currentVariantMember.PrimaryImageUrl;
        var displayVendorProductId = displayOffer?.VendorProductId ?? Guid.Empty;
        var displayUnit = displayOffer?.Unit ?? currentVariantMember.Unit;
        var displayDescription = displayOffer?.Description;
        var displayIsDiscounted = displayOffer?.IsDiscounted ?? false;

        reviewStatsByVendorId.TryGetValue(displayOffer?.VendorId ?? Guid.Empty, out var defaultReviewStats);

        return new ProductDetailsDto(
            masterProductId,
            masterProductId,
            displayVendorProductId,
            displayName,
            displayStore,
            displayPrice,
            displayOldPrice,
            displayImageUrl,
            galleryImages,
            defaultReviewStats?.AverageRating,
            defaultReviewStats?.ReviewCount ?? 0,
            FormatDiscount(displayPrice, displayOldPrice),
            false,
            displayUnit,
            displayIsDiscounted,
            displayDescription,
            isOnlineNow,
            isAvailableForPurchase,
            unavailableReason,
            variantOptions,
            vendorPrices,
            similarProducts);
    }

    private async Task<List<VariantGroupMemberRow>> LoadVariantGroupMembersAsync(
        Guid variantGroupId,
        Guid currentMasterProductId,
        CancellationToken cancellationToken)
    {
        var query = _context.MasterProducts
            .AsNoTracking()
            .Where(product =>
                !product.IsDeleted &&
                product.Status != ProductStatus.Discontinued &&
                (variantGroupId != Guid.Empty
                    ? product.VariantGroupId == variantGroupId
                    : product.Id == currentMasterProductId));

        var members = await query
            .Select(product => new
            {
                product.Id,
                product.NameAr,
                product.NameEn,
                product.Status,
                product.MeasurementValue,
                PackageTypeAr = product.PackageType != null ? product.PackageType.NameAr : null,
                PackageTypeEn = product.PackageType != null ? product.PackageType.NameEn : null,
                MeasurementUnitAr = product.MeasurementUnit != null
                    ? product.MeasurementUnit.NameAr
                    : product.UnitOfMeasure != null
                        ? product.UnitOfMeasure.NameAr
                        : null,
                MeasurementUnitEn = product.MeasurementUnit != null
                    ? product.MeasurementUnit.NameEn
                    : product.UnitOfMeasure != null
                        ? product.UnitOfMeasure.NameEn
                        : null,
                MeasurementUnitSymbol = product.MeasurementUnit != null
                    ? product.MeasurementUnit.Symbol
                    : product.UnitOfMeasure != null
                        ? product.UnitOfMeasure.Symbol
                        : null,
                PrimaryImageUrl = product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return members
            .Select(member => new VariantGroupMemberRow(
                member.Id,
                member.Status,
                NormalizeText(member.NameAr),
                NormalizeText(member.NameEn),
                MasterProductDisplayDto.BuildDisplaySize(member.PackageTypeAr, member.MeasurementValue, member.MeasurementUnitAr, member.MeasurementUnitSymbol, true),
                MasterProductDisplayDto.BuildDisplaySize(member.PackageTypeEn, member.MeasurementValue, member.MeasurementUnitEn, member.MeasurementUnitSymbol, false),
                NormalizeText(member.PackageTypeAr),
                NormalizeText(member.PackageTypeEn),
                member.MeasurementValue,
                NormalizeText(member.MeasurementUnitAr),
                NormalizeText(member.MeasurementUnitEn),
                PickLocalizedNullable(
                    MasterProductDisplayDto.BuildLegacyUnit(member.PackageTypeAr, member.MeasurementUnitAr, true),
                    MasterProductDisplayDto.BuildLegacyUnit(member.PackageTypeEn, member.MeasurementUnitEn, false)),
                member.PrimaryImageUrl))
            .ToList();
    }

    private static VariantAvailabilityDecision ResolveVariantAvailability(
        VariantGroupMemberRow member,
        IReadOnlyList<VisibleOfferRow> memberOffers)
    {
        if (member.Status != ProductStatus.Active)
        {
            return new VariantAvailabilityDecision(false, false, "product_inactive");
        }

        var visibleOffers = memberOffers.Where(offer => offer.IsVisibleInCatalog).ToList();
        if (visibleOffers.Count > 0)
        {
            return new VariantAvailabilityDecision(
                true,
                visibleOffers.Any(offer => offer.IsOnlineNow),
                null);
        }

        if (memberOffers.Count == 0)
        {
            return new VariantAvailabilityDecision(false, false, "out_of_stock");
        }

        var representative = memberOffers[0];
        return new VariantAvailabilityDecision(
            false,
            representative.IsOnlineNow,
            representative.UnavailableReason ?? "unavailable");
    }

    private async Task<List<VisibleOfferRow>> LoadCandidateOffersAsync(CancellationToken cancellationToken)
    {
        var offers = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => new RawVisibleOfferRow(
                product.Id,
                product.MasterProductId,
                product.VendorId,
                product.VendorBranchId,
                product.VendorBranch != null && product.VendorBranch.IsPrimary,
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
                    .ToList(),
                product.MasterProduct.ShowPriceOnCard))
            .ToListAsync(cancellationToken);

        var availabilityDecisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            _context,
            offers.Select(offer => offer.VendorId),
            cancellationToken);

        var visibleOffers = RawOffersToVisibleOffers(offers, availabilityDecisions);
        visibleOffers = ApplyUnifiedPricing(visibleOffers);
        return SelectRepresentativeOffers(visibleOffers);
    }

    private List<VisibleOfferRow> RawOffersToVisibleOffers(
        List<RawVisibleOfferRow> raw,
        IReadOnlyDictionary<Guid, VendorCustomerAvailabilityDecision> availabilityDecisions)
    {
        return raw
            .Select(offer =>
            {
                var decision = VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, offer.VendorId);

                return new VisibleOfferRow(
                    offer.VendorProductId,
                    offer.MasterProductId,
                    offer.VendorId,
                    offer.VendorBranchId,
                    offer.IsPrimaryBranch,
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
                    offer.Images.Where(url => !string.IsNullOrWhiteSpace(url)).ToList(),
                    decision.IsVisibleInCatalog,
                    decision.IsPurchasable,
                    decision.IsOnlineNow,
                    decision.ReasonCode,
                    offer.ShowPriceOnCard);
            })
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

    private static Guid GetProductGroupKey(Guid variantGroupId, Guid masterProductId) =>
        variantGroupId != default ? variantGroupId : masterProductId;

    private static List<VisibleOfferRow> ApplyUnifiedPricing(List<VisibleOfferRow> offers)
    {
        var canonicalPricingByProduct = offers
            .GroupBy(offer => new { offer.VendorId, offer.MasterProductId })
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(offer => offer.IsPrimaryBranch)
                    .ThenBy(offer => offer.VendorBranchId.HasValue)
                    .ThenBy(offer => offer.CreatedAtUtc)
                    .First());

        return offers
            .Select(offer =>
            {
                var canonical = canonicalPricingByProduct[new { offer.VendorId, offer.MasterProductId }];
                return offer with
                {
                    Price = canonical.Price,
                    OldPrice = canonical.OldPrice
                };
            })
            .ToList();
    }

    private static List<VisibleOfferRow> SelectRepresentativeOffers(List<VisibleOfferRow> offers) =>
        offers
            .GroupBy(offer => new { offer.VendorId, offer.MasterProductId })
            .Select(group => group
                .OrderByDescending(offer => offer.IsPrimaryBranch)
                .ThenBy(offer => offer.VendorBranchId.HasValue)
                .ThenBy(offer => offer.CreatedAtUtc)
                .First())
            .ToList();

    private sealed record VariantGroupMemberRow(
        Guid Id,
        ProductStatus Status,
        string? NameAr,
        string? NameEn,
        string? DisplaySizeAr,
        string? DisplaySizeEn,
        string? PackageTypeAr,
        string? PackageTypeEn,
        decimal? MeasurementValue,
        string? MeasurementUnitAr,
        string? MeasurementUnitEn,
        string? Unit,
        string? PrimaryImageUrl);

    private sealed record VariantAvailabilityDecision(
        bool IsAvailableForPurchase,
        bool IsOnlineNow,
        string? UnavailableReason);

    private sealed record RawVisibleOfferRow(
        Guid VendorProductId,
        Guid MasterProductId,
        Guid VendorId,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
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
        List<string> Images,
        bool ShowPriceOnCard);

    private sealed record VisibleOfferRow(
        Guid VendorProductId,
        Guid MasterProductId,
        Guid VendorId,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
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
        List<string> Images,
        bool IsVisibleInCatalog,
        bool IsPurchasable,
        bool IsOnlineNow,
        string? UnavailableReason,
        bool ShowPriceOnCard)
    {
        public string? ImageUrl => Images.FirstOrDefault();
        public bool IsDiscounted => OldPrice.HasValue && OldPrice.Value > Price;
    }
}
