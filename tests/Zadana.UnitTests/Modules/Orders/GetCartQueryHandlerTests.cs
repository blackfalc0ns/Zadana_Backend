using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Orders.Queries.GetCart;
using Zadana.Application.Modules.Orders.Queries.GetCartVendors;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Orders;

public class GetCartQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmptyCart_WhenCustomerHasNoCart()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(Guid.NewGuid(), null), null), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.Summary.ItemsCount.Should().Be(0);
        result.Summary.TotalQuantity.Should().Be(0);
        result.Summary.Subtotal.Should().BeNull();
        result.Summary.DiscountAmount.Should().BeNull();
        result.Summary.TotalAmount.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsCartItemsWithVendorPrices()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCartScenarioAsync(context);
        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(setup.UserId, null), null), CancellationToken.None);

        result.Summary.ItemsCount.Should().Be(1);
        result.Summary.TotalQuantity.Should().Be(2);
        result.Summary.Subtotal.Should().BeNull();
        result.Summary.DiscountAmount.Should().BeNull();
        result.Summary.TotalAmount.Should().BeNull();
        result.Summary.IsPricingAvailable.Should().BeFalse();
        result.Items.Should().ContainSingle();
        result.Items[0].ProductId.Should().Be(setup.MasterProduct.Id);
        result.Items[0].Name.Should().Be("Full Cream Milk 1L");
        result.Items[0].Unit.Should().Be("Liter");
        result.Items[0].ImageUrl.Should().Be("https://cdn.test/milk-primary.jpg");
        result.Items[0].VendorPrices.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersVendorPrices_WhenVendorIdIsProvided()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCartScenarioAsync(context);
        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(setup.UserId, null), setup.FirstVendorId), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].VendorPrices.Should().ContainSingle();
        result.Items[0].VendorPrices[0].Name.Should().Be("Green Valley Market");
        result.Items[0].VendorPrices[0].Price.Should().Be(50m);
        result.Summary.Subtotal.Should().Be(120m);
        result.Summary.DiscountAmount.Should().Be(20m);
        result.Summary.TotalAmount.Should().Be(100m);
        result.Summary.IsPricingAvailable.Should().BeTrue();
        result.Summary.CanCheckout.Should().BeTrue();
        result.Summary.CheckoutBlockReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsNullFinancialTotals_WhenSelectedVendorDoesNotPriceAllCartItems()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCartScenarioWithMissingVendorPriceAsync(context);
        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(setup.UserId, null), setup.FirstVendorId), CancellationToken.None);

        result.Summary.ItemsCount.Should().Be(2);
        result.Summary.TotalQuantity.Should().Be(3);
        result.Summary.Subtotal.Should().Be(120m);
        result.Summary.DiscountAmount.Should().Be(20m);
        result.Summary.TotalAmount.Should().Be(100m);
        result.Summary.IsPricingAvailable.Should().BeTrue();
        result.Summary.CanCheckout.Should().BeTrue();
        result.Summary.CheckoutBlockReason.Should().BeNull();
        result.Summary.HasUnavailableItems.Should().BeTrue();
        result.Summary.UnavailableItemsCount.Should().Be(1);
        result.Summary.RequiresUnavailableItemsConfirmation.Should().BeTrue();
        result.Items.Count(item => !item.IsAvailable).Should().Be(1);
        result.Items.Single(item => !item.IsAvailable).AvailabilityStatus.Should().Be("unavailable_at_selected_vendor");
    }

    [Fact]
    public async Task Handle_MarksItemUnavailable_WhenSelectedVendorStockIsBelowCartQuantity()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCartScenarioAsync(context);
        var vendorProduct = context.VendorProducts.Single(product =>
            product.VendorId == setup.FirstVendorId &&
            product.MasterProductId == setup.MasterProduct.Id);
        vendorProduct.UpdateStock(1);
        await context.SaveChangesAsync();

        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(setup.UserId, null), setup.FirstVendorId), CancellationToken.None);

        result.Summary.CanCheckout.Should().BeFalse();
        result.Summary.CheckoutBlockReason.Should().Be("cart_contains_unavailable_items");
        result.Summary.HasUnavailableItems.Should().BeTrue();
        result.Summary.UnavailableItemsCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].IsAvailable.Should().BeFalse();
        result.Items[0].AvailabilityStatus.Should().Be("insufficient_stock");
    }

    [Fact]
    public async Task Handle_DoesNotMarkSelectedVendorItemUnavailable_WhenDefaultAddressDoesNotResolveToBranch()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedBranchScopedCartScenarioAsync(context);
        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(setup.UserId, null), setup.FirstVendorId), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].IsAvailable.Should().BeTrue();
        result.Items[0].AvailabilityStatus.Should().BeNull();
        result.Items[0].VendorPrices.Should().ContainSingle();
        result.Summary.HasUnavailableItems.Should().BeFalse();
        result.Summary.UnavailableItemsCount.Should().Be(0);
        result.Summary.IsPricingAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetCartVendors_ReturnsAllAvailableVendors()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCartScenarioAsync(context);
        var handler = new GetCartVendorsQueryHandler(context);

        var result = await handler.Handle(new GetCartVendorsQuery(CartActor.Create(setup.UserId, null)), CancellationToken.None);

        result.Vendors.Should().HaveCount(2);
        result.Vendors[0].ProductsCount.Should().Be(1);
        result.Vendors.Select(item => item.Name).Should().Contain(["Green Valley Market", "Town Store"]);
    }

    [Fact]
    public async Task GetCartVendors_ReturnsAvailableVendors_EvenWhenCartIsEmpty()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedCatalogOnlyScenarioAsync(context);
        var handler = new GetCartVendorsQueryHandler(context);

        var result = await handler.Handle(new GetCartVendorsQuery(CartActor.Create(Guid.NewGuid(), "guest-1")), CancellationToken.None);

        result.Vendors.Should().HaveCount(2);
        result.Vendors.Select(item => item.Name).Should().Contain(["Green Valley Market", "Town Store"]);
    }

    [Fact]
    public async Task Handle_ReturnsGuestCartItems_WhenGuestIdMatchesAfterTrim()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var setup = await SeedGuestCartScenarioAsync(context);
        var handler = new GetCartQueryHandler(context);

        var result = await handler.Handle(new GetCartQuery(CartActor.Create(null, " guest-1 "), null), CancellationToken.None);

        result.Summary.ItemsCount.Should().Be(1);
        result.Summary.TotalQuantity.Should().Be(2);
        result.Items.Should().ContainSingle();
        result.Items[0].ProductId.Should().Be(setup.MasterProduct.Id);
    }

    private static async Task<CartScenario> SeedCartScenarioAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var seeded = await SeedCatalogOnlyScenarioAsync(context);

        var cart = new Cart(Guid.NewGuid());
        cart.Items.Add(new CartItem(cart.Id, seeded.MasterProduct.Id, seeded.MasterProduct.NameEn, 2));
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        return new CartScenario(cart.UserId, seeded.MasterProduct, seeded.FirstVendorId, seeded.SecondVendorId);
    }

    private static async Task<CatalogScenario> SeedGuestCartScenarioAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var seeded = await SeedCatalogOnlyScenarioAsync(context);

        var cart = new Cart(null, "guest-1");
        cart.Items.Add(new CartItem(cart.Id, seeded.MasterProduct.Id, seeded.MasterProduct.NameEn, 2));
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        return seeded;
    }

    private static async Task<CartScenario> SeedBranchScopedCartScenarioAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var seeded = await SeedCatalogOnlyScenarioAsync(context);
        var cart = new Cart(Guid.NewGuid());
        cart.Items.Add(new CartItem(cart.Id, seeded.MasterProduct.Id, seeded.MasterProduct.NameEn, 2));

        var customerAddress = new CustomerAddress(
            cart.UserId!.Value,
            "Cart Customer",
            "01000000002",
            "Cairo Address",
            AddressLabel.Home,
            city: "Cairo");
        customerAddress.SetAsDefault();

        var branch = new VendorBranch(
            seeded.FirstVendorId,
            "Dammam Branch",
            "DAMMAM-1",
            true,
            "Dammam Address",
            "Eastern",
            "Dammam",
            26.4207m,
            50.0888m,
            "01000000003",
            "Branch Manager",
            "01000000004",
            15m);

        context.Carts.Add(cart);
        context.CustomerAddresses.Add(customerAddress);
        context.VendorBranches.Add(branch);
        await context.SaveChangesAsync();

        var firstVendorWideProduct = context.VendorProducts.Single(product =>
            product.VendorId == seeded.FirstVendorId &&
            product.MasterProductId == seeded.MasterProduct.Id &&
            !product.VendorBranchId.HasValue);
        context.VendorProducts.Remove(firstVendorWideProduct);
        await context.SaveChangesAsync();

        context.VendorProducts.Add(new VendorProduct(
            seeded.FirstVendorId,
            seeded.MasterProduct.Id,
            50m,
            10,
            60m,
            vendorBranchId: branch.Id));
        await context.SaveChangesAsync();

        return new CartScenario(cart.UserId, seeded.MasterProduct, seeded.FirstVendorId, seeded.SecondVendorId);
    }

    private static async Task<CartScenario> SeedCartScenarioWithMissingVendorPriceAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var seeded = await SeedCatalogOnlyScenarioAsync(context);

        var secondProduct = new MasterProduct("eggs-ar", "Farm Eggs 6 pcs", "eggs", seeded.MasterProduct.CategoryId, seeded.MasterProduct.BrandId, seeded.MasterProduct.UnitOfMeasureId);
        secondProduct.Publish();
        secondProduct.AddImage("https://cdn.test/eggs-primary.jpg", displayOrder: 0, isPrimary: true);
        context.MasterProducts.Add(secondProduct);
        await context.SaveChangesAsync();

        context.VendorProducts.Add(new VendorProduct(seeded.SecondVendorId, secondProduct.Id, 30m, 5, null));
        await context.SaveChangesAsync();

        var cart = new Cart(Guid.NewGuid());
        cart.Items.Add(new CartItem(cart.Id, seeded.MasterProduct.Id, seeded.MasterProduct.NameEn, 2));
        cart.Items.Add(new CartItem(cart.Id, secondProduct.Id, secondProduct.NameEn, 1));
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        return new CartScenario(cart.UserId, seeded.MasterProduct, seeded.FirstVendorId, seeded.SecondVendorId);
    }

    private static async Task<CatalogScenario> SeedCatalogOnlyScenarioAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var category = new Category("milk-ar", "Milk", null, null, 1);
        var brand = new Brand("brand-ar", "Almarai", "almarai.png");
        var unit = new UnitOfMeasure("liter-ar", "Liter", "L");
        context.Categories.Add(category);
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var masterProduct = new MasterProduct("milk-ar", "Full Cream Milk 1L", "milk", category.Id, brand.Id, unit.Id);
        masterProduct.Publish();
        masterProduct.AddImage("https://cdn.test/milk-primary.jpg", displayOrder: 0, isPrimary: true);
        context.MasterProducts.Add(masterProduct);
        await context.SaveChangesAsync();

        var firstVendor = CreateActiveVendor("Green Valley Market");
        var secondVendor = CreateActiveVendor("Town Store");
        context.Vendors.AddRange(firstVendor, secondVendor);
        await context.SaveChangesAsync();

        context.VendorProducts.AddRange(
            new VendorProduct(firstVendor.Id, masterProduct.Id, 50m, 10, 60m),
            new VendorProduct(secondVendor.Id, masterProduct.Id, 55m, 8, 65m));
        await context.SaveChangesAsync();
        
        return new CatalogScenario(masterProduct, firstVendor.Id, secondVendor.Id);
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

    private sealed record CartScenario(Guid? UserId, MasterProduct MasterProduct, Guid FirstVendorId, Guid SecondVendorId);
    private sealed record CatalogScenario(MasterProduct MasterProduct, Guid FirstVendorId, Guid SecondVendorId);

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
