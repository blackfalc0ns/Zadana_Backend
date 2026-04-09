using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetCategoryProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetCategoryProductsQueryHandler(context);

        var act = () => handler.Handle(
            new GetCategoryProductsQuery(Guid.NewGuid(), null, null, null, null, null, null, null, 1, 20),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AppliesBrandQuantityPriceFiltersWithinSelectedSubcategory()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("root-ar", "Root", null, null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var subcategory = new Category("milk-ar", "Milk", null, root.Id, 1);
        var otherSubcategory = new Category("yogurt-ar", "Yogurt", null, root.Id, 2);
        context.Categories.AddRange(subcategory, otherSubcategory);
        await context.SaveChangesAsync();

        var brandA = new Brand("brand-a-ar", "Brand A", "a.png");
        var brandB = new Brand("brand-b-ar", "Brand B", "b.png");
        var milkType = new ProductType("milk-type-ar", "Milk", subcategory.Id);
        var yogurtType = new ProductType("yogurt-type-ar", "Yogurt", otherSubcategory.Id);
        var fullCreamPart = new Part("full-cream-ar", "Full Cream", milkType.Id);
        var greekPart = new Part("greek-ar", "Greek", yogurtType.Id);
        var liter = new UnitOfMeasure("liter-ar", "Liter", "L");
        var pack = new UnitOfMeasure("pack-ar", "Pack", "P");
        context.Brands.AddRange(brandA, brandB);
        context.ProductTypes.AddRange(milkType, yogurtType);
        context.Parts.AddRange(fullCreamPart, greekPart);
        context.UnitsOfMeasure.AddRange(liter, pack);
        await context.SaveChangesAsync();

        var matching = new MasterProduct("prod-a-ar", "Alpha Milk", "alpha-milk", subcategory.Id, brandA.Id, liter.Id, productTypeId: milkType.Id, partId: fullCreamPart.Id);
        matching.Publish();
        var wrongBrand = new MasterProduct("prod-b-ar", "Beta Milk", "beta-milk", subcategory.Id, brandB.Id, liter.Id);
        wrongBrand.Publish();
        var wrongUnit = new MasterProduct("prod-c-ar", "Gamma Milk", "gamma-milk", subcategory.Id, brandA.Id, pack.Id);
        wrongUnit.Publish();
        var wrongSubcategory = new MasterProduct("prod-d-ar", "Delta Yogurt", "delta-yogurt", otherSubcategory.Id, brandA.Id, liter.Id, productTypeId: yogurtType.Id, partId: greekPart.Id);
        wrongSubcategory.Publish();
        context.MasterProducts.AddRange(matching, wrongBrand, wrongUnit, wrongSubcategory);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, matching.Id, 25m, 10),
            new VendorProduct(vendor.Id, wrongBrand.Id, 25m, 10),
            new VendorProduct(vendor.Id, wrongUnit.Id, 25m, 10),
            new VendorProduct(vendor.Id, wrongSubcategory.Id, 25m, 10));
        await context.SaveChangesAsync();

        var handler = new GetCategoryProductsQueryHandler(context);

        var result = await handler.Handle(
            new GetCategoryProductsQuery(
                subcategory.Id,
                milkType.Id,
                fullCreamPart.Id,
                liter.Id,
                brandA.Id,
                20m,
                30m,
                null,
                1,
                20),
            CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Name.Should().Be("Alpha Milk");
        result.AppliedFilters.ProductTypeId.Should().Be(milkType.Id);
        result.AppliedFilters.PartId.Should().Be(fullCreamPart.Id);
        result.AppliedFilters.BrandId.Should().Be(brandA.Id);
        result.AppliedFilters.QuantityId.Should().Be(liter.Id);
    }

    [Fact]
    public async Task Handle_AppliesSortingAndPagination()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("root-ar", "Root", null, null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var brand = new Brand("brand-ar", "Brand", "brand.png");
        var unit = new UnitOfMeasure("unit-ar", "Unit", "U");
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var p1 = new MasterProduct("one-ar", "Zeta", "zeta", root.Id, brand.Id, unit.Id);
        var p2 = new MasterProduct("two-ar", "Alpha", "alpha", root.Id, brand.Id, unit.Id);
        var p3 = new MasterProduct("three-ar", "Gamma", "gamma", root.Id, brand.Id, unit.Id);
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

        var handler = new GetCategoryProductsQueryHandler(context);

        var alphabetical = await handler.Handle(
            new GetCategoryProductsQuery(root.Id, null, null, null, null, null, null, "alphabetical", 1, 2),
            CancellationToken.None);

        alphabetical.Total.Should().Be(3);
        alphabetical.Items.Select(item => item.Name).Should().Equal("Alpha", "Gamma");

        var bestSelling = await handler.Handle(
            new GetCategoryProductsQuery(root.Id, null, null, null, null, null, null, "best_selling", 1, 3),
            CancellationToken.None);

        bestSelling.Items.First().Name.Should().Be("Zeta");
    }

    [Fact]
    public async Task Handle_WhenParentCategoryIsSelected_IncludesProductsFromActiveSubcategories()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("food-ar", "Food", null, null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var milk = new Category("milk-ar", "Milk", null, root.Id, 1);
        var cheese = new Category("cheese-ar", "Cheese", null, root.Id, 2);
        context.Categories.AddRange(milk, cheese);
        await context.SaveChangesAsync();

        var brand = new Brand("brand-ar", "Brand", "brand.png");
        var unit = new UnitOfMeasure("unit-ar", "Unit", "U");
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var milkProduct = new MasterProduct("milk-prod-ar", "Milk Product", "milk-product", milk.Id, brand.Id, unit.Id);
        var cheeseProduct = new MasterProduct("cheese-prod-ar", "Cheese Product", "cheese-product", cheese.Id, brand.Id, unit.Id);
        milkProduct.Publish();
        cheeseProduct.Publish();
        context.MasterProducts.AddRange(milkProduct, cheeseProduct);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Store One");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, milkProduct.Id, 10m, 10),
            new VendorProduct(vendor.Id, cheeseProduct.Id, 15m, 10));
        await context.SaveChangesAsync();

        var handler = new GetCategoryProductsQueryHandler(context);

        var result = await handler.Handle(
            new GetCategoryProductsQuery(root.Id, null, null, null, null, null, null, "alphabetical", 1, 20),
            CancellationToken.None);

        result.Total.Should().Be(2);
        result.Items.Select(item => item.Name).Should().Equal("Cheese Product", "Milk Product");
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
