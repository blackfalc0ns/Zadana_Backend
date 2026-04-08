using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        context.Notifications.Add(new Notification(customer.Id, "n1", "b1"));
        context.Notifications.Add(new Notification(customer.Id, "n2", "b2"));
        var readNotification = new Notification(customer.Id, "n3", "b3");
        readNotification.MarkAsRead();
        context.Notifications.Add(readNotification);
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService(customer.Id, true));

        var result = await service.GetHeaderAsync();

        result.Should().BeEquivalentTo(new HomeHeaderDto("المنزل", "Maadi, Cairo", "Street 2", 2));
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

        var service = new HomeReadService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetBannersAsync(10);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Active title");
    }

    [Fact]
    public async Task GetSpecialOffersAsync_ReturnsOnlyDiscountedProducts()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context);
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetSpecialOffersAsync(10);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(setup.DiscountedProduct.Id);
        result[0].IsDiscounted.Should().BeTrue();
        result[0].OldPrice.Should().Be(15m);
        result[0].Discount.Should().Be("33%");
    }

    [Fact]
    public async Task GetRecommendedAsync_WithDeliveredHistory_PrefersMatchingCategoryOrBrand()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, includeHistory: true);
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService(setup.Customer.Id, true));

        var result = await service.GetRecommendedAsync(1);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(setup.HistoryMatchedProduct!.Id);
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
            vendorProductId: setup.DiscountedProduct.Id,
            note: "vendor placement"));
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetFeaturedProductsAsync(5);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(setup.DiscountedProduct.Id);
        result[0].IsFeatured.Should().BeTrue();
        result[1].Id.Should().Be(setup.OtherProduct!.Id);
        result[1].IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task GetContentAsync_ReturnsDynamicSectionsForActiveSubCategories()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, createSubcategoryData: true);
        context.HomeSections.Add(new HomeSection(setup.DynamicSectionCategoryId!.Value, "theme1", 1, 6));
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetContentAsync();

        result.Sections.Should().ContainSingle();
        result.Sections[0].SubcategoryId.Should().Be(setup.DynamicSectionCategoryId.Value);
        result.Sections[0].Theme.Should().Be("theme1");
        result.Sections[0].Title.Should().Be("Fruits");
        result.Sections[0].Products.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetContentAsync_WhenDynamicSectionsSettingDisabled_ReturnsEmptySections()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogScenarioAsync(context, createSubcategoryData: true);
        context.HomeSections.Add(new HomeSection(setup.DynamicSectionCategoryId!.Value, "theme1", 1, 6));
        context.HomeContentSectionSettings.Add(new HomeContentSectionSetting(HomeContentSectionType.DynamicSections, false));
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService(null, false));

        var result = await service.GetContentAsync();

        result.Sections.Should().BeEmpty();
    }

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
        var brand = new Brand("المراعي", "Almarai", "almarai.png");
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

        HomeProductReference? historyMatchedProduct = null;
        if (includeHistory)
        {
            var address = new CustomerAddress(customer.Id, "Customer", "0102", "History Address", AddressLabel.Home, city: "Cairo", area: "Maadi");
            context.CustomerAddresses.Add(address);
            await context.SaveChangesAsync();

            var deliveredOrder = new Order("ORD-1", customer.Id, vendor.Id, address.Id, PaymentMethodType.CashOnDelivery, 8m, 0m, 0m, 0m);
            deliveredOrder.ChangeStatus(OrderStatus.Delivered);
            context.Orders.Add(deliveredOrder);
            await context.SaveChangesAsync();

            context.OrderItems.Add(new OrderItem(deliveredOrder.Id, historicalProduct.Id, historicalMaster.Id, "Labneh", 2, 8m, unitName: "Liter"));
            historyMatchedProduct = new HomeProductReference(discountedProduct.Id);
        }

        return new HomeScenario(
            customer,
            new HomeProductReference(discountedProduct.Id),
            historyMatchedProduct,
            otherMaster.Id,
            new HomeProductReference(otherProduct.Id),
            subCategory?.Id);
    }

    private static User CreateAdmin(string email) => new("Admin", email, "01000000000", UserRole.SuperAdmin);
    private static User CreateCustomer(string email) => new("Customer", email, "01000000001", UserRole.Customer);
    private static User CreateVendorUser(string email) => new("Vendor", email, "01000000002", UserRole.Vendor);

    private sealed record HomeScenario(
        User Customer,
        HomeProductReference DiscountedProduct,
        HomeProductReference? HistoryMatchedProduct,
        Guid OtherMasterProductId,
        HomeProductReference? OtherProduct,
        Guid? DynamicSectionCategoryId);
    private sealed record HomeProductReference(Guid Id);

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid? userId, bool isAuthenticated)
        {
            UserId = userId;
            IsAuthenticated = isAuthenticated;
        }

        public Guid? UserId { get; }
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
}
