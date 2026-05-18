using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class SearchProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenQueryIsNull_ReturnsBrowsableProductsWithoutCollapsingIndependentProducts()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var category = new Category("cat-ar", "Category", null, null, 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var first = new MasterProduct("alpha-ar", "Alpha Milk", "alpha-milk", category.Id);
        var second = new MasterProduct("beta-ar", "Beta Bread", "beta-bread", category.Id);
        first.Publish();
        second.Publish();
        context.MasterProducts.AddRange(first, second);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, first.Id, 10m, 10),
            new VendorProduct(vendor.Id, second.Id, 12m, 10));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new SearchProductsQuery(null, null, null, null, null, "alphabetical", 1, 20),
            CancellationToken.None);

        result.Query.Should().BeEmpty();
        result.Total.Should().Be(2);
        result.Items.Select(item => item.Name).Should().Equal("Alpha Milk", "Beta Bread");
        result.Items.Should().OnlyContain(item => item.VariantCount == 1);
    }

    [Fact]
    public async Task Handle_WhenVariantsShareGroup_ReturnsSingleCardWithVariantCount()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var category = new Category("cat-ar", "Category", null, null, 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var variantGroupId = Guid.NewGuid();
        var small = new MasterProduct("small-ar", "Small Milk", "small-milk", category.Id, variantGroupId: variantGroupId);
        var large = new MasterProduct("large-ar", "Large Milk", "large-milk", category.Id, variantGroupId: variantGroupId);
        small.Publish();
        large.Publish();
        context.MasterProducts.AddRange(small, large);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, small.Id, 8m, 10),
            new VendorProduct(vendor.Id, large.Id, 12m, 10));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new SearchProductsQuery("Milk", null, null, null, null, "price_low_high", 1, 20),
            CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(small.Id);
        result.Items[0].VariantCount.Should().Be(2);
    }

    private static SearchProductsQueryHandler CreateHandler(Infrastructure.Persistence.ApplicationDbContext context) =>
        new(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

    private static Vendor CreateActiveVendor(string businessNameEn)
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            "vendor-ar",
            businessNameEn,
            "groceries",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "01000000001");

        vendor.Approve(10m, Guid.NewGuid());
        return vendor;
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
