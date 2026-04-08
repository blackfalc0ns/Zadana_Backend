using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Home.DTOs;
using Zadana.Application.Modules.Home.Interfaces;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Infrastructure.Modules.Home.Services;

public class HomeReadService : IHomeReadService
{
    private const int DefaultBannerTake = 5;
    private const int DefaultCategoryTake = 8;
    private const int DefaultBrandTake = 10;
    private const int DefaultProductTake = 10;
    private const int MaxTake = 20;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public HomeReadService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public Task<HomeHeaderDto> GetHeaderAsync(CancellationToken cancellationToken = default) =>
        BuildHeaderAsync(cancellationToken);

    public async Task<HomeContentDto> GetContentAsync(CancellationToken cancellationToken = default)
    {
        var sectionSettings = await LoadSectionSettingsAsync(cancellationToken);
        var header = await BuildHeaderAsync(cancellationToken);
        var needsCatalog =
            IsSectionEnabled(sectionSettings, HomeContentSectionType.SpecialOffers) ||
            IsSectionEnabled(sectionSettings, HomeContentSectionType.Recommended) ||
            IsSectionEnabled(sectionSettings, HomeContentSectionType.BestSelling) ||
            IsSectionEnabled(sectionSettings, HomeContentSectionType.Brands) ||
            IsSectionEnabled(sectionSettings, HomeContentSectionType.FeaturedProducts) ||
            IsSectionEnabled(sectionSettings, HomeContentSectionType.ExploreMore) ||
            IsSectionEnabled(sectionSettings, HomeContentSectionType.DynamicSections);

        var catalog = needsCatalog
            ? await BuildProductCatalogAsync(cancellationToken)
            : new HomeProductCatalog([], _currentUserService.UserId);

        var banners = IsSectionEnabled(sectionSettings, HomeContentSectionType.Banners)
            ? await GetBannersInternalAsync(DefaultBannerTake, cancellationToken)
            : [];
        var categories = IsSectionEnabled(sectionSettings, HomeContentSectionType.Categories)
            ? await GetCategoriesInternalAsync(DefaultCategoryTake, cancellationToken)
            : [];
        var specialOffers = IsSectionEnabled(sectionSettings, HomeContentSectionType.SpecialOffers)
            ? SelectSpecialOffers(catalog.Products, DefaultProductTake)
            : [];
        var featuredProducts = IsSectionEnabled(sectionSettings, HomeContentSectionType.FeaturedProducts)
            ? await GetFeaturedProductsInternalAsync(catalog.Products, DefaultProductTake, cancellationToken)
            : [];
        var bestSelling = IsSectionEnabled(sectionSettings, HomeContentSectionType.BestSelling)
            ? SelectBestSelling(catalog.Products, DefaultProductTake)
            : [];
        var recommended = IsSectionEnabled(sectionSettings, HomeContentSectionType.Recommended)
            ? await SelectRecommendedAsync(catalog, DefaultProductTake, cancellationToken)
            : [];

        var excludedExploreIds = featuredProducts
            .Select(x => x.Id)
            .Concat(recommended.Select(x => x.Id))
            .ToHashSet();

        var brands = IsSectionEnabled(sectionSettings, HomeContentSectionType.Brands)
            ? SelectBrands(catalog.Products, DefaultBrandTake)
            : [];
        var exploreMore = IsSectionEnabled(sectionSettings, HomeContentSectionType.ExploreMore)
            ? SelectExploreMore(catalog.Products, DefaultProductTake, excludedExploreIds)
            : [];
        var sections = IsSectionEnabled(sectionSettings, HomeContentSectionType.DynamicSections)
            ? await GetDynamicSectionsInternalAsync(catalog.Products, cancellationToken)
            : [];

        return new HomeContentDto(
            header.DeliverToLabel,
            header.Location,
            header.AddressLine,
            header.NotificationsCount,
            banners,
            categories,
            specialOffers,
            recommended,
            bestSelling,
            brands,
            featuredProducts,
            exploreMore,
            sections);
    }

    public Task<IReadOnlyList<HomeBannerDto>> GetBannersAsync(int take, CancellationToken cancellationToken = default) =>
        GetBannersOrEmptyAsync(NormalizeTake(take, DefaultBannerTake), cancellationToken);

    public Task<IReadOnlyList<HomeCategoryDto>> GetCategoriesAsync(int take, CancellationToken cancellationToken = default) =>
        GetCategoriesOrEmptyAsync(NormalizeTake(take, DefaultCategoryTake), cancellationToken);

    public async Task<IReadOnlyList<HomeProductCardDto>> GetSpecialOffersAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.SpecialOffers, cancellationToken))
        {
            return [];
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return SelectSpecialOffers(catalog.Products, NormalizeTake(take, DefaultProductTake));
    }

    public async Task<IReadOnlyList<HomeProductCardDto>> GetRecommendedAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.Recommended, cancellationToken))
        {
            return [];
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return await SelectRecommendedAsync(catalog, NormalizeTake(take, DefaultProductTake), cancellationToken);
    }

    public async Task<IReadOnlyList<HomeProductCardDto>> GetBestSellingAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.BestSelling, cancellationToken))
        {
            return [];
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return SelectBestSelling(catalog.Products, NormalizeTake(take, DefaultProductTake));
    }

    public async Task<IReadOnlyList<HomeBrandCardDto>> GetBrandsAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.Brands, cancellationToken))
        {
            return [];
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return SelectBrands(catalog.Products, NormalizeTake(take, DefaultBrandTake));
    }

    public async Task<IReadOnlyList<HomeProductCardDto>> GetFeaturedProductsAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.FeaturedProducts, cancellationToken))
        {
            return [];
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return await GetFeaturedProductsInternalAsync(catalog.Products, NormalizeTake(take, DefaultProductTake), cancellationToken);
    }

    public async Task<IReadOnlyList<HomeProductCardDto>> GetExploreMoreAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.ExploreMore, cancellationToken))
        {
            return [];
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return SelectExploreMore(catalog.Products, NormalizeTake(take, DefaultProductTake), null);
    }

    private async Task<IReadOnlyList<HomeBannerDto>> GetBannersOrEmptyAsync(int take, CancellationToken cancellationToken)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.Banners, cancellationToken))
        {
            return [];
        }

        return await GetBannersInternalAsync(take, cancellationToken);
    }

    private async Task<IReadOnlyList<HomeCategoryDto>> GetCategoriesOrEmptyAsync(int take, CancellationToken cancellationToken)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.Categories, cancellationToken))
        {
            return [];
        }

        return await GetCategoriesInternalAsync(take, cancellationToken);
    }

    private async Task<HomeHeaderDto> BuildHeaderAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!_currentUserService.IsAuthenticated || !userId.HasValue)
        {
            return new HomeHeaderDto(string.Empty, string.Empty, string.Empty, 0);
        }

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var notificationsCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId.Value && !x.IsRead, cancellationToken);

        if (address is null)
        {
            return new HomeHeaderDto(string.Empty, string.Empty, string.Empty, notificationsCount);
        }

        var deliverToLabel = LocalizeAddressLabel(address.Label);
        var location = BuildLocation(address.Area, address.City, address.AddressLine);
        var addressLine = address.AddressLine?.Trim() ?? string.Empty;

        return new HomeHeaderDto(deliverToLabel, location, addressLine, notificationsCount);
    }

    private async Task<IReadOnlyList<HomeBannerDto>> GetBannersInternalAsync(int take, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var banners = await _context.HomeBanners
            .AsNoTracking()
            .Where(x => x.IsActive
                && (!x.StartsAtUtc.HasValue || x.StartsAtUtc <= now)
                && (!x.EndsAtUtc.HasValue || x.EndsAtUtc >= now))
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new RawHomeBanner(
                x.Id,
                x.TagAr,
                x.TagEn,
                x.TitleAr,
                x.TitleEn,
                x.SubtitleAr,
                x.SubtitleEn,
                x.ActionLabelAr,
                x.ActionLabelEn,
                x.ImageUrl))
            .ToListAsync(cancellationToken);

        return banners
            .Select(x => new HomeBannerDto(
                x.Id,
                PickLocalized(x.TagAr, x.TagEn),
                PickLocalized(x.TitleAr, x.TitleEn),
                PickLocalizedNullable(x.SubtitleAr, x.SubtitleEn),
                PickLocalizedNullable(x.ActionLabelAr, x.ActionLabelEn),
                x.ImageUrl))
            .ToList();
    }

    private async Task<IReadOnlyList<HomeCategoryDto>> GetCategoriesInternalAsync(int take, CancellationToken cancellationToken) =>
        (await _context.Categories
            .AsNoTracking()
            .Where(x => x.IsActive && x.ParentCategoryId == null)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.NameAr)
            .Take(take)
            .Select(x => new RawHomeCategory(
                x.Id,
                x.NameAr,
                x.NameEn,
                x.ImageUrl))
            .ToListAsync(cancellationToken))
        .Select(x => new HomeCategoryDto(
            x.Id,
            PickLocalized(x.NameAr, x.NameEn),
            x.ImageUrl))
        .ToList();

    private async Task<HomeProductCatalog> BuildProductCatalogAsync(CancellationToken cancellationToken)
    {
        var salesByVendorProductId = await _context.OrderItems
            .AsNoTracking()
            .Where(x => x.Order.Status == OrderStatus.Delivered)
            .GroupBy(x => x.VendorProductId)
            .Select(x => new { VendorProductId = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToDictionaryAsync(x => x.VendorProductId, x => x.Quantity, cancellationToken);

        var reviewStatsByVendorId = await _context.Reviews
            .AsNoTracking()
            .GroupBy(x => x.VendorId)
            .Select(x => new
            {
                VendorId = x.Key,
                AverageRating = Math.Round(x.Average(y => y.Rating), 1),
                ReviewCount = x.Count()
            })
            .ToDictionaryAsync(x => x.VendorId, x => new VendorReviewStats((decimal)x.AverageRating, x.ReviewCount), cancellationToken);

        var rawProducts = await _context.VendorProducts
            .AsNoTracking()
            .Where(vp =>
                vp.Status == VendorProductStatus.Active &&
                vp.IsAvailable &&
                vp.StockQuantity > 0 &&
                vp.MasterProduct.Status == ProductStatus.Active &&
                vp.Vendor.Status == VendorStatus.Active &&
                vp.Vendor.AcceptOrders)
            .Select(vp => new RawHomeProduct(
                vp.Id,
                vp.CreatedAtUtc,
                vp.VendorId,
                vp.MasterProductId,
                vp.MasterProduct.CategoryId,
                vp.MasterProduct.BrandId,
                vp.MasterProduct.Brand != null && vp.MasterProduct.Brand.IsActive,
                !string.IsNullOrWhiteSpace(vp.CustomNameAr) ? vp.CustomNameAr : vp.MasterProduct.NameAr,
                !string.IsNullOrWhiteSpace(vp.CustomNameEn) ? vp.CustomNameEn : vp.MasterProduct.NameEn,
                vp.Vendor.BusinessNameAr,
                vp.Vendor.BusinessNameEn,
                vp.SellingPrice,
                vp.CompareAtPrice,
                vp.MasterProduct.UnitOfMeasure != null ? vp.MasterProduct.UnitOfMeasure.NameAr : null,
                vp.MasterProduct.UnitOfMeasure != null ? vp.MasterProduct.UnitOfMeasure.NameEn : null,
                vp.MasterProduct.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault(),
                vp.MasterProduct.Brand != null ? vp.MasterProduct.Brand.NameAr : null,
                vp.MasterProduct.Brand != null ? vp.MasterProduct.Brand.NameEn : null,
                vp.MasterProduct.Brand != null ? vp.MasterProduct.Brand.LogoUrl : null))
            .ToListAsync(cancellationToken);

        var products = rawProducts
            .Select(x =>
            {
                salesByVendorProductId.TryGetValue(x.Id, out var salesCount);
                reviewStatsByVendorId.TryGetValue(x.VendorId, out var reviewStats);

                return new HomeProductSource(
                    x.Id,
                    x.CreatedAtUtc,
                    x.VendorId,
                    x.MasterProductId,
                    x.CategoryId,
                    x.BrandId,
                    x.BrandIsActive,
                    PickLocalized(x.NameAr, x.NameEn),
                    PickLocalized(x.StoreAr, x.StoreEn),
                    x.SellingPrice,
                    x.CompareAtPrice,
                    PickLocalizedNullable(x.UnitAr, x.UnitEn),
                    x.ImageUrl ?? string.Empty,
                    salesCount,
                    reviewStats?.AverageRating,
                    reviewStats?.ReviewCount ?? 0,
                    x.BrandId.HasValue ? PickLocalizedNullable(x.BrandNameAr, x.BrandNameEn) : null,
                    x.BrandLogo);
            })
            .ToList();

        return new HomeProductCatalog(products, _currentUserService.UserId);
    }

    private IReadOnlyList<HomeProductCardDto> SelectSpecialOffers(IEnumerable<HomeProductSource> products, int take) =>
        products
            .Where(x => x.CompareAtPrice.HasValue && x.CompareAtPrice.Value > x.SellingPrice)
            .OrderByDescending(x => CalculateDiscountRate(x))
            .ThenByDescending(x => x.SalesCount)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => MapToProductCard(x))
            .ToList();

    private IReadOnlyList<HomeProductCardDto> SelectBestSelling(IEnumerable<HomeProductSource> products, int take) =>
        products
            .OrderByDescending(x => x.SalesCount)
            .ThenByDescending(x => x.Rating ?? 0)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => MapToProductCard(x))
            .ToList();

    private IReadOnlyList<HomeProductCardDto> SelectFeaturedProducts(IEnumerable<HomeProductSource> products, int take) =>
        products
            .OrderByDescending(x => x.Rating ?? 0)
            .ThenByDescending(x => x.ReviewCount)
            .ThenByDescending(x => x.SalesCount)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => MapToProductCard(x, true))
            .ToList();

    private async Task<IReadOnlyList<HomeProductCardDto>> GetFeaturedProductsInternalAsync(
        IReadOnlyList<HomeProductSource> products,
        int take,
        CancellationToken cancellationToken)
    {
        var placements = await GetActiveFeaturedPlacementsAsync(cancellationToken);
        if (placements.Count == 0)
        {
            return SelectFeaturedProducts(products, take);
        }

        var curated = ResolveFeaturedPlacements(products, placements, take);
        return curated.Count > 0
            ? curated
            : SelectFeaturedProducts(products, take);
    }

    private async Task<List<ActiveFeaturedPlacement>> GetActiveFeaturedPlacementsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return await _context.FeaturedProductPlacements
            .AsNoTracking()
            .Where(x => x.IsActive
                && (!x.StartsAtUtc.HasValue || x.StartsAtUtc <= now)
                && (!x.EndsAtUtc.HasValue || x.EndsAtUtc >= now))
            .OrderBy(x => x.PlacementType == FeaturedPlacementType.VendorProduct ? 0 : 1)
            .ThenBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new ActiveFeaturedPlacement(
                x.PlacementType,
                x.VendorProductId,
                x.MasterProductId))
            .ToListAsync(cancellationToken);
    }

    private IReadOnlyList<HomeProductCardDto> ResolveFeaturedPlacements(
        IReadOnlyList<HomeProductSource> products,
        IReadOnlyList<ActiveFeaturedPlacement> placements,
        int take)
    {
        var byVendorProductId = products.ToDictionary(x => x.Id);
        var groupedByMasterProduct = products
            .GroupBy(x => x.MasterProductId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(p => p.Rating ?? 0)
                    .ThenByDescending(p => p.SalesCount)
                    .ThenByDescending(p => p.CreatedAtUtc)
                    .ToList());

        var result = new List<HomeProductCardDto>();
        var seenVendorProductIds = new HashSet<Guid>();

        foreach (var placement in placements)
        {
            if (result.Count >= take)
            {
                break;
            }

            HomeProductSource? product = null;

            if (placement.PlacementType == FeaturedPlacementType.VendorProduct && placement.VendorProductId.HasValue)
            {
                byVendorProductId.TryGetValue(placement.VendorProductId.Value, out product);
            }
            else if (placement.PlacementType == FeaturedPlacementType.MasterProduct && placement.MasterProductId.HasValue)
            {
                if (groupedByMasterProduct.TryGetValue(placement.MasterProductId.Value, out var candidates))
                {
                    product = candidates.FirstOrDefault(x => !seenVendorProductIds.Contains(x.Id));
                }
            }

            if (product is null || !seenVendorProductIds.Add(product.Id))
            {
                continue;
            }

            result.Add(MapToProductCard(product, true));
        }

        return result;
    }

    private async Task<IReadOnlyList<HomeProductCardDto>> SelectRecommendedAsync(
        HomeProductCatalog catalog,
        int take,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !catalog.CurrentUserId.HasValue)
        {
            return SelectFeaturedProducts(catalog.Products, take);
        }

        var purchasedMasterProducts = await _context.OrderItems
            .AsNoTracking()
            .Where(x => x.Order.UserId == catalog.CurrentUserId.Value && x.Order.Status == OrderStatus.Delivered)
            .Join(
                _context.MasterProducts.AsNoTracking(),
                orderItem => orderItem.MasterProductId,
                masterProduct => masterProduct.Id,
                (orderItem, masterProduct) => new { masterProduct.CategoryId, masterProduct.BrandId })
            .ToListAsync(cancellationToken);

        if (purchasedMasterProducts.Count == 0)
        {
            return SelectFeaturedProducts(catalog.Products, take);
        }

        var categoryIds = purchasedMasterProducts
            .Select(x => x.CategoryId)
            .ToHashSet();

        var brandIds = purchasedMasterProducts
            .Where(x => x.BrandId.HasValue)
            .Select(x => x.BrandId!.Value)
            .ToHashSet();

        var recommended = catalog.Products
            .Select(x => new
            {
                Product = x,
                Score =
                    (categoryIds.Contains(x.CategoryId) ? 2 : 0) +
                    (x.BrandId.HasValue && brandIds.Contains(x.BrandId.Value) ? 1 : 0)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Product.Rating ?? 0)
            .ThenByDescending(x => x.Product.SalesCount)
            .ThenByDescending(x => x.Product.CreatedAtUtc)
            .Take(take)
            .Select(x => MapToProductCard(x.Product))
            .ToList();

        return recommended.Any()
            ? recommended
            : SelectFeaturedProducts(catalog.Products, take);
    }

    private IReadOnlyList<HomeBrandCardDto> SelectBrands(IEnumerable<HomeProductSource> products, int take) =>
        products
            .Where(x => x.BrandId.HasValue && x.BrandIsActive)
            .GroupBy(x => new { x.BrandId, x.BrandName, x.BrandLogo })
            .Select(x => new HomeBrandCardDto(
                x.Key.BrandId!.Value,
                x.Key.BrandName ?? string.Empty,
                x.Key.BrandLogo,
                null,
                x.Select(y => y.Id).Distinct().Count(),
                null))
            .OrderByDescending(x => x.ProductCount)
            .ThenBy(x => x.Name)
            .Take(take)
            .ToList();

    private IReadOnlyList<HomeProductCardDto> SelectExploreMore(
        IEnumerable<HomeProductSource> products,
        int take,
        ISet<Guid>? excludedIds)
    {
        var query = products
            .Where(x => excludedIds == null || !excludedIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.SalesCount)
            .ThenBy(x => x.Name)
            .Take(take)
            .Select(x => MapToProductCard(x))
            .ToList();

        if (query.Count >= take || excludedIds == null)
        {
            return query;
        }

        var additionalItems = products
            .Where(x => !query.Select(y => y.Id).Contains(x.Id))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.SalesCount)
            .Take(take - query.Count)
            .Select(x => MapToProductCard(x))
            .ToList();

        return query.Concat(additionalItems).ToList();
    }

    private async Task<IReadOnlyList<HomeDynamicSectionDto>> GetDynamicSectionsInternalAsync(
        IReadOnlyList<HomeProductSource> products,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var sections = await _context.HomeSections
            .AsNoTracking()
            .Where(x => x.IsActive
                && x.Category.IsActive
                && x.Category.ParentCategoryId != null
                && (!x.StartsAtUtc.HasValue || x.StartsAtUtc <= now)
                && (!x.EndsAtUtc.HasValue || x.EndsAtUtc >= now))
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new ActiveHomeSection(
                x.Id,
                x.CategoryId,
                x.Theme,
                x.ProductsTake,
                x.Category.NameAr,
                x.Category.NameEn))
            .ToListAsync(cancellationToken);

        return sections
            .Select(section => new HomeDynamicSectionDto(
                section.Id,
                section.CategoryId,
                PickLocalized(section.CategoryNameAr, section.CategoryNameEn),
                section.Theme,
                products
                    .Where(x => x.CategoryId == section.CategoryId)
                    .OrderByDescending(x => x.Rating ?? 0)
                    .ThenByDescending(x => x.SalesCount)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(section.ProductsTake)
                    .Select(x => MapToProductCard(x))
                    .ToList()))
            .Where(x => x.Products.Count > 0)
            .ToList();
    }

    private HomeProductCardDto MapToProductCard(HomeProductSource product, bool isFeatured = false)
    {
        var isDiscounted = product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.SellingPrice;

        return new HomeProductCardDto(
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
            isFeatured,
            product.Unit,
            isDiscounted);
    }

    private async Task<Dictionary<HomeContentSectionType, bool>> LoadSectionSettingsAsync(CancellationToken cancellationToken)
    {
        var savedSettings = await _context.HomeContentSectionSettings
            .AsNoTracking()
            .ToDictionaryAsync(x => x.SectionType, x => x.IsEnabled, cancellationToken);

        var result = new Dictionary<HomeContentSectionType, bool>();
        foreach (var sectionType in Enum.GetValues<HomeContentSectionType>())
        {
            result[sectionType] = savedSettings.TryGetValue(sectionType, out var isEnabled) ? isEnabled : true;
        }

        return result;
    }

    private async Task<bool> IsSectionEnabledAsync(HomeContentSectionType sectionType, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeContentSectionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SectionType == sectionType, cancellationToken);

        return entity?.IsEnabled ?? true;
    }

    private static bool IsSectionEnabled(
        IReadOnlyDictionary<HomeContentSectionType, bool> sectionSettings,
        HomeContentSectionType sectionType) =>
        sectionSettings.TryGetValue(sectionType, out var isEnabled) ? isEnabled : true;

    private static string BuildLocation(string? area, string? city, string addressLine)
    {
        var parts = new[] { area, city }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();

        return parts.Length > 0
            ? string.Join(", ", parts)
            : addressLine;
    }

    private static int NormalizeTake(int take, int defaultValue)
    {
        if (take <= 0)
        {
            return defaultValue;
        }

        return Math.Min(take, MaxTake);
    }

    private bool IsArabic() =>
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

    private string LocalizeAddressLabel(AddressLabel? label)
    {
        if (!label.HasValue)
        {
            return string.Empty;
        }

        return label.Value switch
        {
            AddressLabel.Home => IsArabic() ? "المنزل" : "Home",
            AddressLabel.Work => IsArabic() ? "العمل" : "Work",
            _ => IsArabic() ? "أخرى" : "Other"
        };
    }

    private static decimal CalculateDiscountRate(HomeProductSource product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= 0 || product.CompareAtPrice.Value <= product.SellingPrice)
        {
            return 0;
        }

        return (product.CompareAtPrice.Value - product.SellingPrice) / product.CompareAtPrice.Value;
    }

    private static string? FormatDiscount(HomeProductSource product)
    {
        var rate = CalculateDiscountRate(product);
        if (rate <= 0)
        {
            return null;
        }

        return $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
    }

    private sealed record HomeProductCatalog(IReadOnlyList<HomeProductSource> Products, Guid? CurrentUserId);

    private sealed record VendorReviewStats(decimal AverageRating, int ReviewCount);

    private sealed record RawHomeProduct(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid VendorId,
        Guid MasterProductId,
        Guid CategoryId,
        Guid? BrandId,
        bool BrandIsActive,
        string? NameAr,
        string? NameEn,
        string StoreAr,
        string StoreEn,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? UnitAr,
        string? UnitEn,
        string? ImageUrl,
        string? BrandNameAr,
        string? BrandNameEn,
        string? BrandLogo);

    private sealed record RawHomeBanner(
        Guid Id,
        string TagAr,
        string TagEn,
        string TitleAr,
        string TitleEn,
        string? SubtitleAr,
        string? SubtitleEn,
        string? ActionLabelAr,
        string? ActionLabelEn,
        string ImageUrl);

    private sealed record RawHomeCategory(
        Guid Id,
        string NameAr,
        string NameEn,
        string? ImageUrl);

    private sealed record HomeProductSource(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid VendorId,
        Guid MasterProductId,
        Guid CategoryId,
        Guid? BrandId,
        bool BrandIsActive,
        string Name,
        string Store,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? Unit,
        string ImageUrl,
        int SalesCount,
        decimal? Rating,
        int ReviewCount,
        string? BrandName,
        string? BrandLogo);

    private sealed record ActiveFeaturedPlacement(
        FeaturedPlacementType PlacementType,
        Guid? VendorProductId,
        Guid? MasterProductId);

    private sealed record ActiveHomeSection(
        Guid Id,
        Guid CategoryId,
        string Theme,
        int ProductsTake,
        string CategoryNameAr,
        string CategoryNameEn);
}
