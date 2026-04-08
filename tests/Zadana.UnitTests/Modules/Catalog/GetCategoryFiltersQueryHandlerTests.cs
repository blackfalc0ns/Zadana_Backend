using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryFilters;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetCategoryFiltersQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetCategoryFiltersQueryHandler(context);

        var act = () => handler.Handle(new GetCategoryFiltersQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCategoryIsInactive_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var category = new Category("category-ar", "Category", null, null, 1);
        category.Deactivate();
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var handler = new GetCategoryFiltersQueryHandler(context);

        var act = () => handler.Handle(new GetCategoryFiltersQuery(category.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReturnsLocalizedDirectSubcategories_AndLeafFilterMetadata()
    {
        using var scope = new CultureScope("ar");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("\u0623\u0644\u0628\u0627\u0646", "Dairy", null, null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var childOne = new Category("\u062D\u0644\u064A\u0628", "Milk", null, root.Id, 1);
        var childTwo = new Category("\u0632\u0628\u0627\u062F\u064A", "Yogurt", null, root.Id, 2);
        var inactiveChild = new Category("\u062C\u0628\u0646\u0629", "Cheese", null, root.Id, 3);
        inactiveChild.Deactivate();
        context.Categories.AddRange(childOne, childTwo, inactiveChild);
        await context.SaveChangesAsync();

        var grandChild = new Category("\u0643\u0627\u0645\u0644 \u0627\u0644\u062F\u0633\u0645", "Full Fat", null, childOne.Id, 1);
        context.Categories.Add(grandChild);
        await context.SaveChangesAsync();

        var handler = new GetCategoryFiltersQueryHandler(context);

        var result = await handler.Handle(new GetCategoryFiltersQuery(root.Id), CancellationToken.None);

        result.Category.Name.Should().Be("\u0623\u0644\u0628\u0627\u0646");
        result.Subcategories.Select(item => item.Id).Should().Equal(childOne.Id, childTwo.Id);
        result.Subcategories.Select(item => item.Name).Should().Equal(
            "\u062D\u0644\u064A\u0628",
            "\u0632\u0628\u0627\u062F\u064A");
        result.Subcategories.Should().NotContain(item => item.Id == grandChild.Id || item.Id == inactiveChild.Id);
        result.ProductTypes.Should().BeEmpty();
        result.Parts.Should().BeEmpty();
        result.SortOptions.Select(option => option.Value).Should().Equal(
            "newest",
            "price_low_high",
            "price_high_low",
            "best_selling",
            "highest_rated",
            "alphabetical");
        result.SortOptions[0].Label.Should().Be("\u0627\u0644\u0623\u062D\u062F\u062B");
    }

    [Fact]
    public async Task Handle_ReturnsBrandsQuantitiesAndPriceRange_FromActiveSubtree()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("root-ar", "Root", null, null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var child = new Category("child-ar", "Child", null, root.Id, 1);
        var grandChild = new Category("grand-ar", "Grand", null, child.Id, 1);
        context.Categories.AddRange(child, grandChild);
        await context.SaveChangesAsync();

        var activeBrand = new Brand("brand-ar", "FreshCo", "freshco.png");
        var inactiveBrand = new Brand("inactive-brand-ar", "HiddenBrand", "hidden.png");
        inactiveBrand.Deactivate();
        var liter = new UnitOfMeasure("liter-ar", "Liter", "L");
        var piece = new UnitOfMeasure("piece-ar", "Piece", "pc");
        var inactiveUnit = new UnitOfMeasure("inactive-unit-ar", "Hidden Unit", "hu");
        inactiveUnit.Deactivate();
        context.Brands.AddRange(activeBrand, inactiveBrand);
        context.UnitsOfMeasure.AddRange(liter, piece, inactiveUnit);
        await context.SaveChangesAsync();

        var rootProduct = new MasterProduct("milk-ar", "Milk", "milk", root.Id, activeBrand.Id, liter.Id);
        rootProduct.Publish();
        var childProduct = new MasterProduct("yogurt-ar", "Yogurt", "yogurt", child.Id, activeBrand.Id, piece.Id);
        childProduct.Publish();
        var grandProduct = new MasterProduct("cream-ar", "Cream", "cream", grandChild.Id, activeBrand.Id, liter.Id);
        grandProduct.Publish();
        var inactiveBrandProduct = new MasterProduct("hidden-ar", "Hidden", "hidden", root.Id, inactiveBrand.Id, liter.Id);
        inactiveBrandProduct.Publish();
        var inactiveUnitProduct = new MasterProduct("hidden-unit-ar", "Hidden Unit Product", "hidden-unit", root.Id, activeBrand.Id, inactiveUnit.Id);
        inactiveUnitProduct.Publish();
        context.MasterProducts.AddRange(rootProduct, childProduct, grandProduct, inactiveBrandProduct, inactiveUnitProduct);
        await context.SaveChangesAsync();

        var activeVendor = CreateActiveVendor("Active Vendor");
        var inactiveVendor = new Vendor(
            Guid.NewGuid(),
            "inactive-vendor-ar",
            "Inactive Vendor",
            "groceries",
            "CR-2",
            "inactive@example.com",
            "01000000002");
        context.Vendors.AddRange(activeVendor, inactiveVendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(activeVendor.Id, rootProduct.Id, 25m, 10),
            new VendorProduct(activeVendor.Id, childProduct.Id, 40m, 8),
            new VendorProduct(activeVendor.Id, grandProduct.Id, 15m, 6),
            new VendorProduct(activeVendor.Id, inactiveBrandProduct.Id, 99m, 5),
            new VendorProduct(activeVendor.Id, inactiveUnitProduct.Id, 88m, 5),
            new VendorProduct(inactiveVendor.Id, rootProduct.Id, 5m, 10),
            new VendorProduct(activeVendor.Id, rootProduct.Id, 120m, 0));
        await context.SaveChangesAsync();

        var handler = new GetCategoryFiltersQueryHandler(context);

        var result = await handler.Handle(new GetCategoryFiltersQuery(root.Id), CancellationToken.None);

        result.Brands.Should().ContainSingle();
        result.Brands[0].Name.Should().Be("FreshCo");
        result.Brands[0].LogoUrl.Should().Be("freshco.png");

        result.Quantities.Select(item => item.Name).Should().Equal("Liter", "Piece");

        result.PriceRange.Min.Should().Be(15m);
        result.PriceRange.Max.Should().Be(99m);
    }

    [Fact]
    public async Task Handle_ForLeafCategory_ReturnsEmptySubcategories()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var leaf = new Category("leaf-ar", "Leaf", null, null, 1);
        context.Categories.Add(leaf);
        await context.SaveChangesAsync();

        var handler = new GetCategoryFiltersQueryHandler(context);

        var result = await handler.Handle(new GetCategoryFiltersQuery(leaf.Id), CancellationToken.None);

        result.Subcategories.Should().BeEmpty();
        result.Brands.Should().BeEmpty();
        result.Quantities.Should().BeEmpty();
        result.PriceRange.Min.Should().Be(0);
        result.PriceRange.Max.Should().Be(0);
    }

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
