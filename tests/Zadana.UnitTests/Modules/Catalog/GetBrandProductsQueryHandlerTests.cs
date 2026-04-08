using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetBrandProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenBrandDoesNotExist_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetBrandProductsQueryHandler(context);

        var act = () => handler.Handle(new GetBrandProductsQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AppliesCategorySubcategoryUnitAndPriceFilters()
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
        var otherBrand = new Brand("other-ar", "Competitor", null);
        var liter = new UnitOfMeasure("liter-ar", "Liter", "L");
        var pack = new UnitOfMeasure("pack-ar", "Pack", "P");
        context.Brands.AddRange(brand, otherBrand);
        context.UnitsOfMeasure.AddRange(liter, pack);
        await context.SaveChangesAsync();

        var match = new MasterProduct("match-ar", "Milk One", "milk-one", milk.Id, brand.Id, liter.Id);
        var wrongSubcategory = new MasterProduct("wrong-sub-ar", "Yogurt One", "yogurt-one", yogurt.Id, brand.Id, liter.Id);
        var wrongUnit = new MasterProduct("wrong-unit-ar", "Milk Two", "milk-two", milk.Id, brand.Id, pack.Id);
        var wrongBrand = new MasterProduct("wrong-brand-ar", "Milk Three", "milk-three", milk.Id, otherBrand.Id, liter.Id);
        match.Publish();
        wrongSubcategory.Publish();
        wrongUnit.Publish();
        wrongBrand.Publish();
        context.MasterProducts.AddRange(match, wrongSubcategory, wrongUnit, wrongBrand);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, match.Id, 25m, 10),
            new VendorProduct(vendor.Id, wrongSubcategory.Id, 25m, 10),
            new VendorProduct(vendor.Id, wrongUnit.Id, 25m, 10),
            new VendorProduct(vendor.Id, wrongBrand.Id, 25m, 10));
        await context.SaveChangesAsync();

        var handler = new GetBrandProductsQueryHandler(context);

        var result = await handler.Handle(
            new GetBrandProductsQuery(brand.Id, dairy.Id, milk.Id, liter.Id, 20m, 30m, null, 1, 20),
            CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Name.Should().Be("Milk One");
        result.AppliedFilters.CategoryId.Should().Be(dairy.Id);
        result.AppliedFilters.SubcategoryId.Should().Be(milk.Id);
        result.AppliedFilters.UnitId.Should().Be(liter.Id);
    }

    [Fact]
    public async Task Handle_AppliesSortingAndPagination()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var category = new Category("cat-ar", "Category", null, null, 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var brand = new Brand("brand-ar", "Brand", "brand.png");
        var unit = new UnitOfMeasure("unit-ar", "Unit", "U");
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var p1 = new MasterProduct("one-ar", "Zeta", "zeta", category.Id, brand.Id, unit.Id);
        var p2 = new MasterProduct("two-ar", "Alpha", "alpha", category.Id, brand.Id, unit.Id);
        var p3 = new MasterProduct("three-ar", "Gamma", "gamma", category.Id, brand.Id, unit.Id);
        p1.Publish();
        p2.Publish();
        p3.Publish();
        context.MasterProducts.AddRange(p1, p2, p3);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var vp1 = new VendorProduct(vendor.Id, p1.Id, 30m, 10);
        var vp2 = new VendorProduct(vendor.Id, p2.Id, 10m, 10);
        var vp3 = new VendorProduct(vendor.Id, p3.Id, 20m, 10);
        context.VendorProducts.AddRange(vp1, vp2, vp3);

        var order = new Order(
            "ORD-1",
            Guid.NewGuid(),
            vendor.Id,
            Guid.NewGuid(),
            PaymentMethodType.CashOnDelivery,
            80m,
            0m,
            0m,
            0m);
        order.ChangeStatus(OrderStatus.Delivered);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        context.OrderItems.AddRange(
            new OrderItem(order.Id, vp1.Id, p1.Id, p1.NameEn, 2, 30m, unitName: unit.NameEn),
            new OrderItem(order.Id, vp1.Id, p1.Id, p1.NameEn, 1, 30m, unitName: unit.NameEn),
            new OrderItem(order.Id, vp3.Id, p3.Id, p3.NameEn, 1, 20m, unitName: unit.NameEn));
        await context.SaveChangesAsync();

        var handler = new GetBrandProductsQueryHandler(context);

        var alphabetical = await handler.Handle(
            new GetBrandProductsQuery(brand.Id, null, null, null, null, null, "alphabetical", 1, 2),
            CancellationToken.None);

        alphabetical.Total.Should().Be(3);
        alphabetical.Items.Select(item => item.Name).Should().Equal("Alpha", "Gamma");

        var bestSelling = await handler.Handle(
            new GetBrandProductsQuery(brand.Id, null, null, null, null, null, "best_selling", 1, 3),
            CancellationToken.None);

        bestSelling.Items.First().Name.Should().Be("Zeta");
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
