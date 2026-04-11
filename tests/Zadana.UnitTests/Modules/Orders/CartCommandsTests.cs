using FluentAssertions;
using Zadana.Application.Modules.Orders.Commands.AddCartItem;
using Zadana.Application.Modules.Orders.Commands.ClearCart;
using Zadana.Application.Modules.Orders.Commands.RemoveCartItem;
using Zadana.Application.Modules.Orders.Commands.UpdateCartItemQuantity;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Orders;

public class CartCommandsTests
{
    [Fact]
    public async Task AddCartItem_CreatesCartAndReturnsProjectedItem()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedAvailableProductAsync(context);

        var handler = new AddCartItemCommandHandler(context);

        var result = await handler.Handle(new AddCartItemCommand(CartActor.Create(setup.UserId, null), setup.MasterProduct.Id, 2), CancellationToken.None);

        result.Message.Should().Be("added to cart successfully");
        result.Item.ProductId.Should().Be(setup.MasterProduct.Id);
        result.Item.Quantity.Should().Be(2);
        result.Item.VendorPrices.Should().BeEmpty();
        context.Carts.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateCartItemQuantity_UpdatesQuantity()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedCartWithItemAsync(context);

        var handler = new UpdateCartItemQuantityCommandHandler(context);

        var result = await handler.Handle(new UpdateCartItemQuantityCommand(CartActor.Create(setup.UserId, null), setup.CartItem.Id, 5), CancellationToken.None);

        result.Message.Should().Be("cart item updated successfully");
        result.Item.Quantity.Should().Be(5);
        result.Summary.TotalQuantity.Should().Be(5);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_ReturnsVendorPricesAndUpdatedTotals_WhenVendorIsSelected()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedCartWithItemAsync(context);

        var handler = new UpdateCartItemQuantityCommandHandler(context);

        var result = await handler.Handle(
            new UpdateCartItemQuantityCommand(CartActor.Create(setup.UserId, null), setup.CartItem.Id, 5, setup.FirstVendorId),
            CancellationToken.None);

        result.Item.Quantity.Should().Be(5);
        result.Item.VendorPrices.Should().ContainSingle();
        result.Item.VendorPrices[0].Price.Should().Be(50m);
        result.Summary.Subtotal.Should().Be(300m);
        result.Summary.DiscountAmount.Should().Be(50m);
        result.Summary.TotalAmount.Should().Be(250m);
    }

    [Fact]
    public async Task RemoveCartItem_RemovesLastItemAndReturnsEmptySummary()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedCartWithItemAsync(context);

        var handler = new RemoveCartItemCommandHandler(context);

        var result = await handler.Handle(new RemoveCartItemCommand(CartActor.Create(setup.UserId, null), setup.CartItem.Id), CancellationToken.None);

        result.Message.Should().Be("cart item removed successfully");
        result.Summary.ItemsCount.Should().Be(0);
        result.Summary.TotalQuantity.Should().Be(0);
        context.Carts.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearCart_RemovesExistingCart()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedCartWithItemAsync(context);

        var handler = new ClearCartCommandHandler(context);

        var result = await handler.Handle(new ClearCartCommand(CartActor.Create(setup.UserId, null)), CancellationToken.None);

        result.Message.Should().Be("cart cleared successfully");
        context.Carts.Should().BeEmpty();
    }

    [Fact]
    public async Task AddCartItem_ReturnsNoVendorPrices_UntilVendorIsSelected()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedAvailableProductAsync(context);

        var handler = new AddCartItemCommandHandler(context);

        var result = await handler.Handle(
            new AddCartItemCommand(CartActor.Create(setup.UserId, null), setup.MasterProduct.Id, 1),
            CancellationToken.None);

        result.Item.VendorPrices.Should().BeEmpty();
    }

    private static async Task<ProductSetup> SeedAvailableProductAsync(Infrastructure.Persistence.ApplicationDbContext context)
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
            new VendorProduct(secondVendor.Id, masterProduct.Id, 55m, 8));
        await context.SaveChangesAsync();

        return new ProductSetup(Guid.NewGuid(), masterProduct, firstVendor.Id, secondVendor.Id);
    }

    private static async Task<CartSetup> SeedCartWithItemAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var productSetup = await SeedAvailableProductAsync(context);
        var cart = new Cart(productSetup.UserId);
        var cartItem = new CartItem(cart.Id, productSetup.MasterProduct.Id, productSetup.MasterProduct.NameEn, 2);
        cart.Items.Add(cartItem);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        return new CartSetup(productSetup.UserId, productSetup.MasterProduct, cartItem, productSetup.FirstVendorId);
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

    private sealed record ProductSetup(Guid UserId, MasterProduct MasterProduct, Guid FirstVendorId, Guid SecondVendorId);

    private sealed record CartSetup(Guid UserId, MasterProduct MasterProduct, CartItem CartItem, Guid FirstVendorId);
}
