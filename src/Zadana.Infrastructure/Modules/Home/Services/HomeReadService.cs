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
            ? await GetBrandsInternalAsync(DefaultBrandTake, cancellationToken)
            : [];
        var exploreMore = IsSectionEnabled(sectionSettings, HomeContentSectionType.ExploreMore)
            ? SelectExploreMore(catalog.Products, DefaultProductTake, excludedExploreIds)
            : [];
        var sections = IsSectionEnabled(sectionSettings, HomeContentSectionType.DynamicSections)
            ? await GetDynamicSectionsInternalAsync(catalog.Products, cancellationToken)
            : [];
        var bannersSection = CreateSection("banners", "Banners", IsSectionEnabled(sectionSettings, HomeContentSectionType.Banners), banners);
        var categoriesSection = CreateSection("categories", "Categories", IsSectionEnabled(sectionSettings, HomeContentSectionType.Categories), categories);
        var specialOffersSection = CreateSection("special_offers", "Special Offers", IsSectionEnabled(sectionSettings, HomeContentSectionType.SpecialOffers), specialOffers);
        var recommendedSection = CreateSection("recommended", "Recommended", IsSectionEnabled(sectionSettings, HomeContentSectionType.Recommended), recommended);
        var bestSellingSection = CreateSection("best_selling", "Best Selling", IsSectionEnabled(sectionSettings, HomeContentSectionType.BestSelling), bestSelling);
        var brandsSection = CreateSection("brands", "Brands", IsSectionEnabled(sectionSettings, HomeContentSectionType.Brands), brands);
        var featuredProductsSection = CreateSection("featured_products", "Featured Products", IsSectionEnabled(sectionSettings, HomeContentSectionType.FeaturedProducts), featuredProducts);
        var exploreMoreSection = CreateSection("explore_more", "Explore More", IsSectionEnabled(sectionSettings, HomeContentSectionType.ExploreMore), exploreMore);

        return new HomeContentDto(
            header.DeliverToLabel,
            header.Location,
            header.AddressLine,
            header.NotificationsCount,
            bannersSection,
            categoriesSection,
            specialOffersSection,
            recommendedSection,
            bestSellingSection,
            brandsSection,
            featuredProductsSection,
            exploreMoreSection,
            sections);
    }

    public async Task<HomeListSectionDto<HomeBannerDto>> GetBannersAsync(int take, CancellationToken cancellationToken = default)
    {
        var items = await GetBannersOrEmptyAsync(NormalizeTake(take, DefaultBannerTake), cancellationToken);
        return CreateSection("banners", "Banners", await IsSectionEnabledAsync(HomeContentSectionType.Banners, cancellationToken), items);
    }

    public async Task<HomeListSectionDto<HomeCategoryDto>> GetCategoriesAsync(int take, CancellationToken cancellationToken = default)
    {
        var items = await GetCategoriesOrEmptyAsync(NormalizeTake(take, DefaultCategoryTake), cancellationToken);
        return CreateSection("categories", "Categories", await IsSectionEnabledAsync(HomeContentSectionType.Categories, cancellationToken), items);
    }

    public async Task<HomeListSectionDto<HomeProductCardDto>> GetSpecialOffersAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.SpecialOffers, cancellationToken))
        {
            return CreateSection("special_offers", "Special Offers", false, Array.Empty<HomeProductCardDto>());
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return CreateSection(
            "special_offers",
            "Special Offers",
            true,
            SelectSpecialOffers(catalog.Products, NormalizeTake(take, DefaultProductTake)));
    }

    public async Task<HomeListSectionDto<HomeProductCardDto>> GetRecommendedAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.Recommended, cancellationToken))
        {
            return CreateSection("recommended", "Recommended", false, Array.Empty<HomeProductCardDto>());
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return CreateSection(
            "recommended",
            "Recommended",
            true,
            await SelectRecommendedAsync(catalog, NormalizeTake(take, DefaultProductTake), cancellationToken));
    }

    public async Task<HomeListSectionDto<HomeProductCardDto>> GetBestSellingAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.BestSelling, cancellationToken))
        {
            return CreateSection("best_selling", "Best Selling", false, Array.Empty<HomeProductCardDto>());
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return CreateSection(
            "best_selling",
            "Best Selling",
            true,
            SelectBestSelling(catalog.Products, NormalizeTake(take, DefaultProductTake)));
    }

    public async Task<HomeListSectionDto<HomeBrandCardDto>> GetBrandsAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.Brands, cancellationToken))
        {
            return CreateSection("brands", "Brands", false, Array.Empty<HomeBrandCardDto>());
        }

        return CreateSection(
            "brands",
            "Brands",
            true,
            await GetBrandsInternalAsync(NormalizeTake(take, DefaultBrandTake), cancellationToken));
    }

    public async Task<HomeListSectionDto<HomeProductCardDto>> GetFeaturedProductsAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.FeaturedProducts, cancellationToken))
        {
            return CreateSection("featured_products", "Featured Products", false, Array.Empty<HomeProductCardDto>());
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return CreateSection(
            "featured_products",
            "Featured Products",
            true,
            await GetFeaturedProductsInternalAsync(catalog.Products, NormalizeTake(take, DefaultProductTake), cancellationToken));
    }

    public async Task<HomeListSectionDto<HomeProductCardDto>> GetExploreMoreAsync(int take, CancellationToken cancellationToken = default)
    {
        if (!await IsSectionEnabledAsync(HomeContentSectionType.ExploreMore, cancellationToken))
        {
            return CreateSection("explore_more", "Explore More", false, Array.Empty<HomeProductCardDto>());
        }

        var catalog = await BuildProductCatalogAsync(cancellationToken);
        return CreateSection(
            "explore_more",
            "Explore More",
            true,
            SelectExploreMore(catalog.Products, NormalizeTake(take, DefaultProductTake), null));
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
            .Where(x => x.IsActive
                && x.ParentCategoryId != null
                && x.ParentCategory != null
                && x.ParentCategory.ParentCategoryId != null
                && x.ParentCategory.ParentCategory != null
                && x.ParentCategory.ParentCategory.ParentCategoryId == null)
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
                    x.MasterProductId,
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
            .GroupBy(x => x.MasterProductId)
            .Select(group => group
                .OrderBy(x => x.SellingPrice)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Store, StringComparer.CurrentCultureIgnoreCase)
                .First())
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
        var byVendorProductId = products.ToDictionary(x => x.VendorProductId);
        var groupedByMasterProduct = products
            .GroupBy(x => x.MasterProductId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(p => p.Rating ?? 0)
                    .ThenByDescending(p => p.SalesCount)
                    .ThenByDescending(p => p.CreatedAtUtc)
                    .ToList());

        var result = new List<HomeProductCardDto>();
        var seenMasterProductIds = new HashSet<Guid>();

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
                    product = candidates.FirstOrDefault(x => !seenMasterProductIds.Contains(x.Id));
                }
            }

            if (product is null || !seenMasterProductIds.Add(product.Id))
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

    private async Task<IReadOnlyList<HomeBrandCardDto>> GetBrandsInternalAsync(int take, CancellationToken cancellationToken)
    {
        var brands = await _context.Brands
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.NameAr,
                x.NameEn,
                x.LogoUrl,
                ProductCount = x.MasterProducts.Count()
            })
            .ToListAsync(cancellationToken);

        return brands
            .Select(x => new HomeBrandCardDto(
                x.Id,
                PickLocalized(x.NameAr, x.NameEn),
                x.LogoUrl,
                null,
                x.ProductCount,
                null))
            .OrderByDescending(x => x.ProductCount)
            .ThenBy(x => x.Name)
            .Take(take)
            .ToList();
    }

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

        List<ActiveHomeSection> sections;
        try
        {
            sections = await _context.HomeSections
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
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            return [];
        }

        return sections
            .Select(section =>
            {
                var items = products
                    .Where(x => x.CategoryId == section.CategoryId)
                    .OrderByDescending(x => x.Rating ?? 0)
                    .ThenByDescending(x => x.SalesCount)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(section.ProductsTake)
                    .Select(x => MapToProductCard(x))
                    .ToList();

                return new HomeDynamicSectionDto(
                    section.Id,
                    "dynamic_section",
                    PickLocalized(section.CategoryNameAr, section.CategoryNameEn),
                    section.CategoryId,
                    true,
                    section.Theme,
                    items.Count,
                    items);
            })
            .Where(x => x.Items.Count > 0)
            .ToList();
    }

    private static HomeListSectionDto<TItem> CreateSection<TItem>(
        string key,
        string title,
        bool isActive,
        IReadOnlyList<TItem> items,
        string? theme = null) =>
        new(
            key,
            title,
            isActive,
            theme,
            items.Count,
            items);

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
        Dictionary<HomeContentSectionType, bool> savedSettings;
        try
        {
            savedSettings = await _context.HomeContentSectionSettings
                .AsNoTracking()
                .ToDictionaryAsync(x => x.SectionType, x => x.IsEnabled, cancellationToken);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            return CreateDefaultSectionSettings(dynamicSectionsEnabled: false);
        }

        var result = CreateDefaultSectionSettings(dynamicSectionsEnabled: true);
        foreach (var setting in savedSettings)
        {
            result[setting.Key] = setting.Value;
        }

        return result;
    }

    private async Task<bool> IsSectionEnabledAsync(HomeContentSectionType sectionType, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _context.HomeContentSectionSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SectionType == sectionType, cancellationToken);

            return entity?.IsEnabled ?? GetDefaultSectionEnabled(sectionType, dynamicSectionsEnabled: true);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            return GetDefaultSectionEnabled(sectionType, dynamicSectionsEnabled: false);
        }
    }

    private static bool IsSectionEnabled(
        IReadOnlyDictionary<HomeContentSectionType, bool> sectionSettings,
        HomeContentSectionType sectionType) =>
        sectionSettings.TryGetValue(sectionType, out var isEnabled) ? isEnabled : true;

    private static Dictionary<HomeContentSectionType, bool> CreateDefaultSectionSettings(bool dynamicSectionsEnabled)
    {
        var defaults = new Dictionary<HomeContentSectionType, bool>();
        foreach (var sectionType in Enum.GetValues<HomeContentSectionType>())
        {
            defaults[sectionType] = GetDefaultSectionEnabled(sectionType, dynamicSectionsEnabled);
        }

        return defaults;
    }

    private static bool GetDefaultSectionEnabled(HomeContentSectionType sectionType, bool dynamicSectionsEnabled) =>
        sectionType == HomeContentSectionType.DynamicSections
            ? dynamicSectionsEnabled
            : true;

    private static bool IsMissingDatabaseObject(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
        Guid VendorProductId,
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
