using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Home.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Home.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Home;

public class HomeReadServiceTests
{
    [Fact]
    public async Task GetHeaderAsync_WithDefaultAddressAndUnreadNotifications_ReturnsLocalizedHeader()
    {
        using var scope = new CultureScope("ar");
        await using var context = TestDbContextFactory.Create();

        var customer = CreateCustomer("header@test.com");
        context.Users.Add(customer);

        var newerAddress = new CustomerAddress(customer.Id, "Ahmed", "0101", "Street 1", AddressLabel.Work, city: "Cairo", area: "Nasr City");
        var defaultAddress = new CustomerAddress(customer.Id, "Ahmed", "0101", "Street 2", AddressLabel.Home, city: "Cairo", area: "Maadi");
        defaultAddress.SetAsDefault();

        context.CustomerAddresses.AddRange(newerAddress, defaultAddress);
        context.Notifications.Add(new Notification(customer.Id, "n1", "n1", "b1", "b1"));
        context.Notifications.Add(new Notification(customer.Id, "n2", "n2", "b2", "b2"));
        var readNotification = new Notification(customer.Id, "n3", "n3", "b3", "b3");
        readNotification.MarkAsRead();
        context.Notifications.Add(readNotification);
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(customer.Id, true));

        var result = await service.GetHeaderAsync();

        result.Should().BeEquivalentTo(new HomeHeaderDto("Customer", "header@test.com", "المنزل", "Maadi, Cairo", "Street 2", 2));
    }

    [Fact]
    public async Task GetBannersAsync_FiltersInactiveAndExpiredItems()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        context.HomeBanners.AddRange(
            new HomeBanner("جديد", "New", "عنوان فعال", "Active title", "/a.jpg", displayOrder: 1, startsAtUtc: DateTime.UtcNow.AddDays(-1), endsAtUtc: DateTime.UtcNow.AddDays(2)),
            CreateInactiveBanner(),
            new HomeBanner("قديم", "Old", "منتهي", "Expired", "/c.jpg", displayOrder: 3, startsAtUtc: DateTime.UtcNow.AddDays(-3), endsAtUtc: DateTime.UtcNow.AddDays(-1)));

        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetBannersAsync(10);

        result.Key.Should().Be("banners");
        result.IsActive.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Active title");
    }

    [Fact]
    public async Task GetBannersAsync_WithArabicCulture_ReturnsArabicBannerContent()
    {
        using var scope = new CultureScope("ar");
        await using var context = TestDbContextFactory.Create();

        context.HomeBanners.Add(new HomeBanner(
            "عروض",
            "Deals",
            "خصومات على المنتجات اليومية",
            "Discounts on daily essentials",
            "/a.jpg",
            subtitleAr: "توصيل سريع وأسعار أفضل",
            subtitleEn: "Fast delivery and better prices",
            actionLabelAr: "تسوق الآن",
            actionLabelEn: "Shop now",
            displayOrder: 1,
            startsAtUtc: DateTime.UtcNow.AddDays(-1),
            endsAtUtc: DateTime.UtcNow.AddDays(2)));

        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetBannersAsync(10);

        result.Title.Should().Be("لافتات");
        result.Items.Should().ContainSingle();
        result.Items[0].Tag.Should().Be("عروض");
        result.Items[0].Title.Should().Be("خصومات على المنتجات اليومية");
        result.Items[0].Subtitle.Should().Be("توصيل سريع وأسعار أفضل");
        result.Items[0].ActionLabel.Should().Be("تسوق الآن");
    }

    [Fact]
    public async Task GetBannersAsync_WithArabicCulture_LocalizesBannerItemsAfterCacheFactory()
    {
        using var scope = new CultureScope("ar");
        await using var context = TestDbContextFactory.Create();

        const string arabicTag = "\u0639\u0631\u0648\u0636";
        const string arabicTitle = "\u062e\u0635\u0648\u0645\u0627\u062a \u064a\u0648\u0645\u064a\u0629";
        const string arabicSubtitle = "\u062a\u0648\u0635\u064a\u0644 \u0633\u0631\u064a\u0639";
        const string arabicActionLabel = "\u062a\u0633\u0648\u0642 \u0627\u0644\u0622\u0646";

        context.HomeBanners.Add(new HomeBanner(
            arabicTag,
            "Deals",
            arabicTitle,
            "Daily discounts",
            "/a.jpg",
            subtitleAr: arabicSubtitle,
            subtitleEn: "Fast delivery",
            actionLabelAr: arabicActionLabel,
            actionLabelEn: "Shop now",
            displayOrder: 1,
            startsAtUtc: DateTime.UtcNow.AddDays(-1),
            endsAtUtc: DateTime.UtcNow.AddDays(2)));

        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            new FakeCurrentUserService(null, false),
            new CultureResettingAppCache("en"));

        var result = await service.GetBannersAsync(10);

        result.Title.Should().Be("\u0644\u0627\u0641\u062a\u0627\u062a");
        result.Items.Should().ContainSingle();
        result.Items[0].Tag.Should().Be(arabicTag);
        result.Items[0].Title.Should().Be(arabicTitle);
        result.Items[0].Subtitle.Should().Be(arabicSubtitle);
        result.Items[0].ActionLabel.Should().Be(arabicActionLabel);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsOnlyPenultimateLevelCategories()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("Food", "Food", "food.jpg", null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var level2 = new Category("Fresh", "Fresh", "fresh.jpg", root.Id, 1);
        context.Categories.Add(level2);
        await context.SaveChangesAsync();

        var level3 = new Category("لحوم", "Meat", "meat.jpg", level2.Id, 1);
        var otherLevel3 = new Category("مخبوزات", "Bakery", "bakery.jpg", level2.Id, 2);
        context.Categories.AddRange(level3, otherLevel3);
        await context.SaveChangesAsync();

        context.Categories.AddRange(
            new Category("لحم بقري", "Beef", "beef.jpg", level3.Id, 1),
            new Category("لحم دجاج", "Chicken", "chicken.jpg", level3.Id, 2),
            new Category("خبز", "Bread", "bread.jpg", otherLevel3.Id, 1));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetCategoriesAsync(10);

        result.Key.Should().Be("categories");
        result.Items.Should().HaveCount(2);
        result.Items.Select(x => x.Name).Should().BeEquivalentTo(["Meat", "Bakery"]);
    }

    [Fact]
    public async Task GetSpecialOffersAsync_ReturnsOnlyDiscountedProducts()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        var secondVendor = new Vendor(
            Guid.NewGuid(),
            "متجر آخر",
            "Other Store",
            "Grocery",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "0500000003");
        secondVendor.Approve(10m, Guid.NewGuid());
        context.Vendors.Add(secondVendor);
        await context.SaveChangesAsync();
        context.VendorProducts.Add(new VendorProduct(secondVendor.Id, setup.DiscountedMasterProductId, 11m, 20, 14m));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetSpecialOffersAsync(10);

        result.Key.Should().Be("special_offers");
        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(setup.DiscountedMasterProductId);
        result.Items[0].IsDiscounted.Should().BeTrue();
        result.Items[0].OldPrice.Should().Be(15m);
        result.Items[0].Discount.Should().Be("33%");
    }

    [Fact]
    public async Task GetRecommendedAsync_WithDeliveredHistory_PrefersMatchingCategoryOrBrand()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, includeHistory: true);
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(setup.Customer.Id, true));

        var result = await service.GetRecommendedAsync(1);

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(setup.HistoryMatchedMasterProductId!.Value);
    }

    [Fact]
    public async Task GetRecommendedAsync_FavoritesReceiveBoostWithinMatchingSignals()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, includeHistory: true);
        context.CustomerFavorites.Add(new CustomerFavorite(setup.Customer.Id, null, setup.DiscountedMasterProductId));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(setup.Customer.Id, true));

        var result = await service.GetRecommendedAsync(2);

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(setup.DiscountedMasterProductId);
        result.Items[0].IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendedAsync_DemotesPreviouslyPurchasedProductsFromTopResults()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, includeHistory: true);
        await AddApprovedVendorProductAsync(context, setup.DiscountedMasterProductId, 9m, 18);
        await AddApprovedVendorProductAsync(context, setup.HistoricalMasterProductId, 7m, 22);
        context.CustomerFavorites.Add(new CustomerFavorite(setup.Customer.Id, null, setup.DiscountedMasterProductId));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(setup.Customer.Id, true));

        var result = await service.GetRecommendedAsync(2);

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(setup.DiscountedMasterProductId);
        result.Items[0].Id.Should().NotBe(setup.HistoricalMasterProductId);
    }

    [Fact]
    public async Task GetBestSellingAsync_BreaksSalesTiesByStoreCount()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        var extraRegular = await AddApprovedVendorProductAsync(context, setup.RegularMasterProductId, 11m, 12);

        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.RegularVendorProductId, setup.RegularMasterProductId, 5, "BEST-A");
        await AddDeliveredSalesAsync(context, setup.Customer.Id, extraRegular.VendorId, extraRegular.Id, setup.RegularMasterProductId, 1, "BEST-B");
        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.OtherVendorProductId, setup.OtherMasterProductId, 6, "BEST-C");

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetBestSellingAsync(2);

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(setup.RegularMasterProductId);
        result.Items[1].Id.Should().Be(setup.OtherMasterProductId);
    }

    [Fact]
    public async Task GetFeaturedProductsAsync_PrefersCuratedVendorThenMasterPlacements()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        context.FeaturedProductPlacements.Add(new FeaturedProductPlacement(
            FeaturedPlacementType.MasterProduct,
            2,
            masterProductId: setup.OtherMasterProductId,
            note: "master placement"));
        context.FeaturedProductPlacements.Add(new FeaturedProductPlacement(
            FeaturedPlacementType.VendorProduct,
            1,
            vendorProductId: setup.DiscountedVendorProductId,
            note: "vendor placement"));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetFeaturedProductsAsync(5);

        result.Key.Should().Be("featured_products");
        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(setup.DiscountedMasterProductId);
        result.Items[0].IsFeatured.Should().BeTrue();
        result.Items[1].Id.Should().Be(setup.OtherMasterProductId);
        result.Items[1].IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task GetFeaturedProductsAsync_ManualPlacementsAreFilledByAutomaticSalesAndStoreRanking()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        context.FeaturedProductSelectionSettings.Add(new FeaturedProductSelectionSettings(
            FeaturedProductSelectionMode.ManualFirstAutoFill,
            targetCount: 3,
            minSalesCount: 0,
            minStoreCount: 1,
            requireDiscount: false,
            excludeProductsAlreadyInSpecialOffers: false));

        context.FeaturedProductPlacements.Add(new FeaturedProductPlacement(
            FeaturedPlacementType.VendorProduct,
            1,
            vendorProductId: setup.DiscountedVendorProductId,
            note: "pinned"));

        var extraVendorProduct = await AddApprovedVendorProductAsync(context, setup.RegularMasterProductId, 11m, 12);
        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.RegularVendorProductId, setup.RegularMasterProductId, 5, "REG-A");
        await AddDeliveredSalesAsync(context, setup.Customer.Id, extraVendorProduct.VendorId, extraVendorProduct.Id, setup.RegularMasterProductId, 1, "REG-B");
        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.OtherVendorProductId, setup.OtherMasterProductId, 5, "OTH-A");

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetFeaturedProductsAsync(10);

        result.Items.Should().HaveCount(3);
        result.Items.Select(x => x.Id).Should().ContainInOrder(
            setup.DiscountedMasterProductId,
            setup.RegularMasterProductId,
            setup.OtherMasterProductId);
    }

    [Fact]
    public async Task GetFeaturedProductsAsync_UsesAutomaticSelectionWhenNoPlacementsExist()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        context.FeaturedProductSelectionSettings.Add(new FeaturedProductSelectionSettings(
            FeaturedProductSelectionMode.ManualFirstAutoFill,
            targetCount: 2,
            minSalesCount: 1,
            minStoreCount: 1,
            requireDiscount: false,
            excludeProductsAlreadyInSpecialOffers: false));

        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.OtherVendorProductId, setup.OtherMasterProductId, 6, "AUTO-A");
        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.RegularVendorProductId, setup.RegularMasterProductId, 4, "AUTO-B");

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetFeaturedProductsAsync(10);

        result.Items.Should().HaveCount(2);
        result.Items.Select(x => x.Id).Should().ContainInOrder(
            setup.OtherMasterProductId,
            setup.RegularMasterProductId);
    }

    [Fact]
    public async Task GetFeaturedProductsAsync_ExcludeSpecialOffersSkipsDiscountedProducts()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        context.FeaturedProductSelectionSettings.Add(new FeaturedProductSelectionSettings(
            FeaturedProductSelectionMode.ManualFirstAutoFill,
            targetCount: 2,
            minSalesCount: 1,
            minStoreCount: 1,
            requireDiscount: false,
            excludeProductsAlreadyInSpecialOffers: true));

        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.DiscountedVendorProductId, setup.DiscountedMasterProductId, 8, "DISC-A");
        await AddDeliveredSalesAsync(context, setup.Customer.Id, setup.PrimaryVendorId, setup.RegularVendorProductId, setup.RegularMasterProductId, 3, "DISC-B");

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetFeaturedProductsAsync(10);

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(setup.RegularMasterProductId);
    }

    [Fact]
    public async Task GetBrandsAsync_ReturnsAllActiveBrandsEvenWhenOnlyOneHasEligibleProducts()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        var extraBrands = new[]
        {
            new Brand("براند 3", "Brand 3", "brand3.png"),
            new Brand("براند 4", "Brand 4", "brand4.png"),
            new Brand("براند 5", "Brand 5", "brand5.png")
        };

        context.Brands.AddRange(extraBrands);
        await context.SaveChangesAsync();

        foreach (var product in context.VendorProducts)
        {
            product.UpdateStock(0);
        }

        var eligibleBrandProduct = await context.VendorProducts.FirstAsync(x => x.Id == setup.DiscountedVendorProductId);
        eligibleBrandProduct.UpdateStock(20);
        eligibleBrandProduct.Activate();
        eligibleBrandProduct.SetAvailability(true);
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetBrandsAsync(10);

        result.Key.Should().Be("brands");
        result.Items.Should().HaveCount(5);
        result.Items.Select(x => x.Name).Should().Contain(["Almarai", "Samsung", "Brand 3", "Brand 4", "Brand 5"]);
        result.Items.First(x => x.Name == "Almarai").ProductCount.Should().Be(2);
        result.Items.First(x => x.Name == "Almarai").CoverImage.Should().Be("almarai-cover.png");
        result.Items.First(x => x.Name == "Samsung").ProductCount.Should().Be(2);
        result.Items.First(x => x.Name == "Brand 3").ProductCount.Should().Be(0);
    }

    [Fact]
    public async Task GetContentAsync_ReturnsDynamicSectionsForActiveSubCategories()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, createSubcategoryData: true);
        context.HomeSections.Add(new HomeSection(setup.DynamicSectionCategoryId!.Value, HomeSectionTheme.SoftBlue, 1, 6));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetContentAsync();

        result.DynamicSections.Should().ContainSingle();
        result.DynamicSections[0].SubcategoryId.Should().Be(setup.DynamicSectionCategoryId.Value);
        result.DynamicSections[0].Theme.Should().Be("theme1");
        result.DynamicSections[0].ThemeLabel.Should().Be("Theme 1");
        result.DynamicSections[0].IsActive.Should().BeTrue();
        result.DynamicSections[0].Title.Should().Be("Fruits");
        result.DynamicSections[0].Items.Should().NotBeEmpty();
        result.DynamicSections[0].ItemsCount.Should().Be(1);
        result.FeaturedProductsSection.Key.Should().Be("featured_products");
    }

    [Fact]
    public async Task GetContentAsync_WhenDynamicSectionsSettingDisabled_ReturnsEmptySections()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, createSubcategoryData: true);
        context.HomeSections.Add(new HomeSection(setup.DynamicSectionCategoryId!.Value, HomeSectionTheme.SoftBlue, 1, 6));
        context.HomeContentSectionSettings.Add(new HomeContentSectionSetting(HomeContentSectionType.DynamicSections, false));
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetContentAsync();

        result.DynamicSections.Should().BeEmpty();
    }

    private static HomeReadService CreateService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAppCache? cache = null) =>
        new(
            context,
            currentUserService,
            cache ?? TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context, currentUserService),
            TestServiceFactory.CreateCachingOptions());

    private static HomeBanner CreateInactiveBanner()
    {
        var banner = new HomeBanner("مخفي", "Hidden", "عنوان مخفي", "Hidden title", "/b.jpg", displayOrder: 2, startsAtUtc: DateTime.UtcNow.AddDays(-1), endsAtUtc: DateTime.UtcNow.AddDays(2));
        banner.Deactivate();
        return banner;
    }

    private static async Task<HomeScenario> SeedCatalogScenarioAsync(
        ApplicationDbContext context,
        bool includeHistory = false,
        bool createSubcategoryData = false)
    {
        var admin = CreateAdmin("admin-home@test.com");
        var customer = CreateCustomer("customer-home@test.com");
        var vendorUser = CreateVendorUser("vendor-home@test.com");

        context.Users.AddRange(admin, customer, vendorUser);

        var category = new Category("ألبان", "Dairy", "cat.jpg", null, 1);
        var otherCategory = new Category("هواتف", "Phones", "phones.jpg", null, 2);
        var subCategory = createSubcategoryData
            ? new Category("فواكه", "Fruits", "fruits.jpg", category.Id, 1)
            : null;
        var brand = new Brand("المراعي", "Almarai", "almarai.png", "almarai-cover.png");
        var otherBrand = new Brand("سامسونج", "Samsung", "samsung.png");
        var unit = new UnitOfMeasure("لتر", "Liter", "L");

        context.Categories.AddRange(category, otherCategory);
        if (subCategory is not null)
        {
            context.Categories.Add(subCategory);
        }
        context.Brands.AddRange(brand, otherBrand);
        context.UnitsOfMeasure.Add(unit);

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر الألبان",
            "Dairy Store",
            "Grocery",
            "CR-1",
            "vendor@test.com",
            "0500000000");
        vendor.Approve(10m, admin.Id);

        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var discountedMaster = new MasterProduct("حليب طازج", "Fresh Milk", "fresh-milk", category.Id, brand.Id, unit.Id);
        discountedMaster.Publish();
        discountedMaster.AddImage("/milk.jpg", isPrimary: true);

        var regularMaster = new MasterProduct("زبادي", "Yogurt", "yogurt", otherCategory.Id, otherBrand.Id, unit.Id);
        regularMaster.Publish();
        regularMaster.AddImage("/yogurt.jpg", isPrimary: true);

        var otherMaster = new MasterProduct("هاتف", "Phone", "phone", otherCategory.Id, otherBrand.Id, unit.Id);
        otherMaster.Publish();
        otherMaster.AddImage("/phone.jpg", isPrimary: true);

        var historicalMaster = new MasterProduct("لبنة", "Labneh", "labneh", category.Id, brand.Id, unit.Id);
        historicalMaster.Publish();
        historicalMaster.AddImage("/labneh.jpg", isPrimary: true);

        MasterProduct? subCategoryMaster = null;
        if (subCategory is not null)
        {
            subCategoryMaster = new MasterProduct("تفاح", "Apple", "apple", subCategory.Id, brand.Id, unit.Id);
            subCategoryMaster.Publish();
            subCategoryMaster.AddImage("/apple.jpg", isPrimary: true);
        }

        context.MasterProducts.AddRange(discountedMaster, regularMaster, otherMaster, historicalMaster);
        if (subCategoryMaster is not null)
        {
            context.MasterProducts.Add(subCategoryMaster);
        }
        await context.SaveChangesAsync();

        var discountedProduct = new VendorProduct(vendor.Id, discountedMaster.Id, 10m, 30, 15m);
        var regularProduct = new VendorProduct(vendor.Id, regularMaster.Id, 12m, 20, null);
        var otherProduct = new VendorProduct(vendor.Id, otherMaster.Id, 20m, 10, null);
        var historicalProduct = new VendorProduct(vendor.Id, historicalMaster.Id, 8m, 0, null);
        VendorProduct? subCategoryProduct = null;
        if (subCategoryMaster is not null)
        {
            subCategoryProduct = new VendorProduct(vendor.Id, subCategoryMaster.Id, 7m, 15, null);
        }

        context.VendorProducts.AddRange(discountedProduct, regularProduct, otherProduct, historicalProduct);
        if (subCategoryProduct is not null)
        {
            context.VendorProducts.Add(subCategoryProduct);
        }
        context.Reviews.Add(new Review(Guid.NewGuid(), customer.Id, vendor.Id, 5, "Great"));

        Guid? historyMatchedMasterProductId = null;
        if (includeHistory)
        {
            var address = new CustomerAddress(customer.Id, "Customer", "0102", "History Address", AddressLabel.Home, city: "Cairo", area: "Maadi");
            context.CustomerAddresses.Add(address);
            await context.SaveChangesAsync();

            var deliveredOrder = new Order("ORD-1", customer.Id, vendor.Id, address.Id, PaymentMethodType.CashOnDelivery, 8m, 0m, 0m, 0m, 0m, 0m, null, null, null, 0m);
            deliveredOrder.ChangeStatus(OrderStatus.Delivered);
            context.Orders.Add(deliveredOrder);
            await context.SaveChangesAsync();

            context.OrderItems.Add(new OrderItem(deliveredOrder.Id, historicalProduct.Id, historicalMaster.Id, "Labneh", 2, 8m, unitName: "Liter"));
            historyMatchedMasterProductId = discountedMaster.Id;
        }

        return new HomeScenario(
            customer,
            vendor.Id,
            discountedMaster.Id,
            discountedProduct.Id,
            regularMaster.Id,
            regularProduct.Id,
            historicalMaster.Id,
            historyMatchedMasterProductId,
            otherMaster.Id,
            otherProduct.Id,
            subCategory?.Id);
    }

    private static async Task<VendorProduct> AddApprovedVendorProductAsync(
        ApplicationDbContext context,
        Guid masterProductId,
        decimal sellingPrice,
        int stockQuantity)
    {
        var admin = CreateAdmin($"{Guid.NewGuid():N}@admin.test");
        var vendorUser = CreateVendorUser($"{Guid.NewGuid():N}@vendor.test");
        context.Users.AddRange(admin, vendorUser);

        var vendor = new Vendor(
            vendorUser.Id,
            $"Store {Guid.NewGuid():N}",
            $"Store {Guid.NewGuid():N}",
            "Grocery",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "0500000003");
        vendor.Approve(10m, admin.Id);
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var vendorProduct = new VendorProduct(vendor.Id, masterProductId, sellingPrice, stockQuantity, null);
        context.VendorProducts.Add(vendorProduct);
        await context.SaveChangesAsync();
        return vendorProduct;
    }

    private static async Task AddDeliveredSalesAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid vendorId,
        Guid vendorProductId,
        Guid masterProductId,
        int quantity,
        string orderNumberSuffix)
    {
        var address = new CustomerAddress(customerId, "Customer", "0102", $"Address {orderNumberSuffix}", AddressLabel.Home, city: "Cairo", area: "Maadi");
        context.CustomerAddresses.Add(address);
        await context.SaveChangesAsync();

        var order = new Order(
            $"ORD-{orderNumberSuffix}",
            customerId,
            vendorId,
            address.Id,
            PaymentMethodType.CashOnDelivery,
            8m,
            0m,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            null,
            0m);
        order.ChangeStatus(OrderStatus.Delivered);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        context.OrderItems.Add(new OrderItem(order.Id, vendorProductId, masterProductId, "Product", quantity, 8m, unitName: "Liter"));
        await context.SaveChangesAsync();
    }

    private static User CreateAdmin(string email) => new("Admin", email, "01000000000", UserRole.SuperAdmin);
    private static User CreateCustomer(string email) => new("Customer", email, "01000000001", UserRole.Customer);
    private static User CreateVendorUser(string email) => new("Vendor", email, "01000000002", UserRole.Vendor);

    private sealed record HomeScenario(
        User Customer,
        Guid PrimaryVendorId,
        Guid DiscountedMasterProductId,
        Guid DiscountedVendorProductId,
        Guid RegularMasterProductId,
        Guid RegularVendorProductId,
        Guid HistoricalMasterProductId,
        Guid? HistoryMatchedMasterProductId,
        Guid OtherMasterProductId,
        Guid OtherVendorProductId,
        Guid? DynamicSectionCategoryId);

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid? userId, bool isAuthenticated, string? guestDeviceId = null)
        {
            UserId = userId;
            IsAuthenticated = isAuthenticated;
            GuestDeviceId = guestDeviceId;
        }

        public Guid? UserId { get; }
        public string? GuestDeviceId { get; }
        public string? Role => IsAuthenticated ? "Customer" : null;
        public bool IsAuthenticated { get; }
        public string? GetDeviceInfo() => "test";
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }

    private sealed class CultureResettingAppCache(string factoryCultureName) : IAppCache
    {
        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            AppCacheEntryOptions options,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                var factoryCulture = new CultureInfo(factoryCultureName);
                CultureInfo.CurrentCulture = factoryCulture;
                CultureInfo.CurrentUICulture = factoryCulture;

                return await factory(cancellationToken);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }
    }
}
