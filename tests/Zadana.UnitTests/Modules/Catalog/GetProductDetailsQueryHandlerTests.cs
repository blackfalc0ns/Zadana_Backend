using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetProductDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetProductDetailsQueryHandler(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

        var act = () => handler.Handle(new GetProductDetailsQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReturnsDetails_WhenCalledWithVendorProductId()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedProductScenarioAsync(context);
        var handler = new GetProductDetailsQueryHandler(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

        var result = await handler.Handle(new GetProductDetailsQuery(setup.PrimaryVendorProduct.Id), CancellationToken.None);

        result.Id.Should().Be(setup.PrimaryMasterProduct.Id);
        result.MasterProductId.Should().Be(setup.PrimaryMasterProduct.Id);
        result.DefaultVendorProductId.Should().Be(setup.PrimaryVendorProduct.Id);
        result.Name.Should().Be("Full Cream Milk 1L");
        result.Store.Should().Be("Green Valley Market");
        result.Price.Should().Be(50m);
        result.OldPrice.Should().Be(62.5m);
        result.IsDiscounted.Should().BeTrue();
        result.Discount.Should().Be("20%");
        result.Description.Should().Be("Fresh milk description");
        result.Images.Should().Contain(setup.PrimaryImage).And.Contain(setup.SecondaryImage);
        result.VendorPrices.Should().HaveCount(2);
        result.VendorPrices.Select(item => item.Id).Should().Contain(new[] { setup.PrimaryVendorProduct.Id, setup.SecondaryVendorProduct.Id });
        result.SimilarProducts.Should().ContainSingle();
        result.SimilarProducts[0].Id.Should().Be(setup.SimilarVendorProduct.MasterProductId);
        result.Rating.Should().BeNull();
        result.ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsDetails_WhenCalledWithMasterProductId()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedProductScenarioAsync(context);
        var handler = new GetProductDetailsQueryHandler(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

        var result = await handler.Handle(new GetProductDetailsQuery(setup.PrimaryMasterProduct.Id), CancellationToken.None);

        result.MasterProductId.Should().Be(setup.PrimaryMasterProduct.Id);
        result.DefaultVendorProductId.Should().Be(setup.PrimaryVendorProduct.Id);
        result.Id.Should().Be(setup.PrimaryMasterProduct.Id);
    }

    [Fact]
    public async Task Handle_WhenVendorIsManuallyOffline_ReturnsUnavailableDetailsInsteadOfNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedProductScenarioAsync(context);
        context.VendorWorkspaceStates.Add(new VendorWorkspaceState(
            setup.PrimaryVendor.Id,
            "store-availability",
            "{\"manual_mode\":\"offline\"}"));
        await context.SaveChangesAsync();

        var handler = new GetProductDetailsQueryHandler(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

        var result = await handler.Handle(new GetProductDetailsQuery(setup.PrimaryVendorProduct.Id), CancellationToken.None);

        result.IsAvailableForPurchase.Should().BeFalse();
        result.IsOnlineNow.Should().BeFalse();
        result.UnavailableReason.Should().Be("vendor_offline");
        result.VendorPrices.Should().BeEmpty();
        result.DefaultVendorProductId.Should().Be(setup.PrimaryVendorProduct.Id);
    }

    [Fact]
    public async Task Handle_WhenProductsHaveNoVariantGroup_DoesNotListUnrelatedProductsAsVariants()
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

        var vendor = CreateActiveVendor("Store One", "store-logo.png");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(vendor.Id, first.Id, 10m, 10),
            new VendorProduct(vendor.Id, second.Id, 12m, 10));
        await context.SaveChangesAsync();

        var handler = new GetProductDetailsQueryHandler(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

        var result = await handler.Handle(new GetProductDetailsQuery(first.Id), CancellationToken.None);

        result.VariantOptions.Should().ContainSingle();
        result.VariantOptions[0].Id.Should().Be(first.Id);
        result.SimilarProducts.Should().ContainSingle();
        result.SimilarProducts[0].Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task Handle_WhenVariantGroupHasInactiveMember_EachVariantHasIndependentAvailability()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var category = new Category("milk-ar", "Milk", null, null, 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var variantGroupId = Guid.NewGuid();
        var activeMasterProduct = new MasterProduct(
            "milk-1l-ar",
            "Full Cream Milk 1L",
            "full-cream-milk-1l",
            category.Id,
            variantGroupId: variantGroupId);
        activeMasterProduct.Publish();
        activeMasterProduct.AddImage("https://cdn.test/milk-1l.jpg", displayOrder: 0, isPrimary: true);

        var inactiveMasterProduct = new MasterProduct(
            "milk-2l-ar",
            "Full Cream Milk 2L",
            "full-cream-milk-2l",
            category.Id,
            variantGroupId: variantGroupId);
        inactiveMasterProduct.Publish();
        inactiveMasterProduct.Unpublish();
        inactiveMasterProduct.AddImage("https://cdn.test/milk-2l.jpg", displayOrder: 0, isPrimary: true);

        context.MasterProducts.AddRange(activeMasterProduct, inactiveMasterProduct);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor("Green Valley Market", "green-logo.png");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var activeVendorProduct = new VendorProduct(vendor.Id, activeMasterProduct.Id, 50m, 10);
        var inactiveVendorProduct = new VendorProduct(vendor.Id, inactiveMasterProduct.Id, 90m, 10);
        context.VendorProducts.AddRange(activeVendorProduct, inactiveVendorProduct);
        await context.SaveChangesAsync();

        var handler = new GetProductDetailsQueryHandler(
            context,
            TestServiceFactory.CreateAppCache(),
            TestServiceFactory.CreateCatalogReadCacheService(context),
            TestServiceFactory.CreateCachingOptions());

        var activeResult = await handler.Handle(new GetProductDetailsQuery(activeMasterProduct.Id), CancellationToken.None);

        activeResult.IsAvailableForPurchase.Should().BeTrue();
        activeResult.VariantOptions.Should().HaveCount(2);
        activeResult.VariantOptions.Should().Contain(option =>
            option.Id == activeMasterProduct.Id &&
            option.IsAvailableForPurchase &&
            option.UnavailableReason == null);
        activeResult.VariantOptions.Should().Contain(option =>
            option.Id == inactiveMasterProduct.Id &&
            !option.IsAvailableForPurchase &&
            option.UnavailableReason == "product_inactive");

        var inactiveResult = await handler.Handle(new GetProductDetailsQuery(inactiveMasterProduct.Id), CancellationToken.None);

        inactiveResult.IsAvailableForPurchase.Should().BeFalse();
        inactiveResult.UnavailableReason.Should().Be("product_inactive");
        inactiveResult.VariantOptions.Should().Contain(option =>
            option.Id == activeMasterProduct.Id &&
            option.IsAvailableForPurchase);
    }

    private static async Task<ProductScenario> SeedProductScenarioAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var category = new Category("milk-ar", "Milk", null, null, 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var brand = new Brand("brand-ar", "Almarai", "almarai.png");
        var unit = new UnitOfMeasure("liter-ar", "Liter", "L");
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var primaryMasterProduct = new MasterProduct(
            "milk-ar",
            "Full Cream Milk 1L",
            "full-cream-milk-1l",
            category.Id,
            brand.Id,
            unit.Id,
            "وصف",
            "Fresh milk description");
        primaryMasterProduct.Publish();
        primaryMasterProduct.AddImage("https://cdn.test/milk-primary.jpg", displayOrder: 0, isPrimary: true);
        primaryMasterProduct.AddImage("https://cdn.test/milk-secondary.jpg", displayOrder: 1);

        var similarMasterProduct = new MasterProduct(
            "similar-ar",
            "Skimmed Milk 1L",
            "skimmed-milk-1l",
            category.Id,
            brand.Id,
            unit.Id,
            "وصف 2",
            "Skimmed milk description");
        similarMasterProduct.Publish();
        similarMasterProduct.AddImage("https://cdn.test/milk-similar.jpg", displayOrder: 0, isPrimary: true);

        context.MasterProducts.AddRange(primaryMasterProduct, similarMasterProduct);
        await context.SaveChangesAsync();

        var primaryVendor = CreateActiveVendor("Green Valley Market", "green-logo.png");
        var secondaryVendor = CreateActiveVendor("Town Store", "town-logo.png");
        context.Vendors.AddRange(primaryVendor, secondaryVendor);
        await context.SaveChangesAsync();

        var primaryVendorProduct = new VendorProduct(primaryVendor.Id, primaryMasterProduct.Id, 50m, 10, 62.5m);
        var secondaryVendorProduct = new VendorProduct(secondaryVendor.Id, primaryMasterProduct.Id, 55m, 8, 70m);
        var similarVendorProduct = new VendorProduct(primaryVendor.Id, similarMasterProduct.Id, 40m, 5, 50m);
        context.VendorProducts.AddRange(primaryVendorProduct, secondaryVendorProduct, similarVendorProduct);
        await context.SaveChangesAsync();

        var firstOrder = new Order(
            "ORD-1",
            Guid.NewGuid(),
            primaryVendor.Id,
            Guid.NewGuid(),
            PaymentMethodType.CashOnDelivery,
            50m,
            0m,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            0m);
        firstOrder.ChangeStatus(OrderStatus.Delivered);

        var secondOrder = new Order(
            "ORD-2",
            Guid.NewGuid(),
            primaryVendor.Id,
            Guid.NewGuid(),
            PaymentMethodType.CashOnDelivery,
            50m,
            0m,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            0m);
        secondOrder.ChangeStatus(OrderStatus.Delivered);

        context.Orders.AddRange(firstOrder, secondOrder);
        await context.SaveChangesAsync();

        context.OrderItems.AddRange(
            new OrderItem(firstOrder.Id, primaryVendorProduct.Id, primaryMasterProduct.Id, primaryMasterProduct.NameEn, 2, 50m, unitName: unit.NameEn),
            new OrderItem(secondOrder.Id, primaryVendorProduct.Id, primaryMasterProduct.Id, primaryMasterProduct.NameEn, 1, 50m, unitName: unit.NameEn),
            new OrderItem(firstOrder.Id, similarVendorProduct.Id, similarMasterProduct.Id, similarMasterProduct.NameEn, 1, 40m, unitName: unit.NameEn));

        await context.SaveChangesAsync();

        return new ProductScenario(
            primaryVendor,
            primaryMasterProduct,
            primaryVendorProduct,
            secondaryVendorProduct,
            similarVendorProduct,
            "https://cdn.test/milk-primary.jpg",
            "https://cdn.test/milk-secondary.jpg");
    }

    private static Vendor CreateActiveVendor(string businessNameEn, string logoUrl)
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            "vendor-ar",
            businessNameEn,
            "groceries",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "01000000001",
            logoUrl: logoUrl);

        vendor.Approve(10m, Guid.NewGuid());
        return vendor;
    }

    private sealed record ProductScenario(
        Vendor PrimaryVendor,
        MasterProduct PrimaryMasterProduct,
        VendorProduct PrimaryVendorProduct,
        VendorProduct SecondaryVendorProduct,
        VendorProduct SimilarVendorProduct,
        string PrimaryImage,
        string SecondaryImage);

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
