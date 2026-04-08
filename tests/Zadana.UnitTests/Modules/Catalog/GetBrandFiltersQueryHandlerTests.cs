using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetBrandFiltersQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenBrandDoesNotExist_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetBrandFiltersQueryHandler(context);

        var act = () => handler.Handle(new GetBrandFiltersQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReturnsCategoriesSubcategoriesUnitsAndPriceRange()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var dairy = new Category("dairy-ar", "Dairy", null, null, 1);
        context.Categories.Add(dairy);
        await context.SaveChangesAsync();

        var milk = new Category("milk-ar", "Milk", null, dairy.Id, 1);
        var yogurt = new Category("yogurt-ar", "Yogurt", null, dairy.Id, 2);
        context.Categories.AddRange(milk, yogurt);
        await context.SaveChangesAsync();

        var brand = new Brand("brand-ar", "Almarai", "almarai.png");
        var otherBrand = new Brand("other-ar", "Other", null);
        var liter = new UnitOfMeasure("liter-ar", "Liter", "L");
        var pack = new UnitOfMeasure("pack-ar", "Pack", "P");
        context.Brands.AddRange(brand, otherBrand);
        context.UnitsOfMeasure.AddRange(liter, pack);
        await context.SaveChangesAsync();

        var milkProduct = new MasterProduct("milk-prod-ar", "Fresh Milk", "fresh-milk", milk.Id, brand.Id, liter.Id);
        var yogurtProduct = new MasterProduct("yogurt-prod-ar", "Greek Yogurt", "greek-yogurt", yogurt.Id, brand.Id, pack.Id);
        var foreignProduct = new MasterProduct("foreign-ar", "Foreign", "foreign", milk.Id, otherBrand.Id, liter.Id);
        milkProduct.Publish();
        yogurtProduct.Publish();
        foreignProduct.Publish();
        context.MasterProducts.AddRange(milkProduct, yogurtProduct, foreignProduct);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, milkProduct.Id, 20m, 10),
            new VendorProduct(vendor.Id, yogurtProduct.Id, 35m, 8),
            new VendorProduct(vendor.Id, foreignProduct.Id, 99m, 8));
        await context.SaveChangesAsync();

        var handler = new GetBrandFiltersQueryHandler(context);

        var result = await handler.Handle(new GetBrandFiltersQuery(brand.Id), CancellationToken.None);

        result.Brand.Name.Should().Be("Almarai");
        result.Categories.Select(item => item.Name).Should().ContainSingle().Which.Should().Be("Dairy");
        result.Subcategories.Select(item => item.Name).Should().Equal("Milk", "Yogurt");
        result.Units.Select(item => item.Name).Should().Equal("Liter", "Pack");
        result.PriceRange.Min.Should().Be(20m);
        result.PriceRange.Max.Should().Be(35m);
        result.SortOptions.Select(option => option.Value).Should().Equal(
            "newest",
            "price_low_high",
            "price_high_low",
            "best_selling",
            "highest_rated",
            "alphabetical");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyCollections_WhenBrandHasNoVisibleProducts()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var brand = new Brand("brand-ar", "Solo", null);
        context.Brands.Add(brand);
        await context.SaveChangesAsync();

        var handler = new GetBrandFiltersQueryHandler(context);

        var result = await handler.Handle(new GetBrandFiltersQuery(brand.Id), CancellationToken.None);

        result.Categories.Should().BeEmpty();
        result.Subcategories.Should().BeEmpty();
        result.Units.Should().BeEmpty();
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
