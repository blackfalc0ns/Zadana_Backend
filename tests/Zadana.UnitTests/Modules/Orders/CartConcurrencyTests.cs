using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Zadana.Application.Modules.Orders.Commands.AddCartItem;
using Zadana.Application.Modules.Orders.Commands.ClearCart;
using Zadana.Application.Modules.Orders.Commands.RemoveCartItem;
using Zadana.Application.Modules.Orders.Commands.UpdateCartItemQuantity;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Orders;

public class CartConcurrencyTests
{
    [Fact]
    public async Task AddCartItem_DoesNotCreateDuplicateGuestCarts_WhenTwoRequestsRace()
    {
        using var database = TestDbContextFactory.CreateSqlite();
        var setup = await SeedCatalogAsync(database);

        var firstTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new AddCartItemCommandHandler(context, NullLogger<AddCartItemCommandHandler>.Instance);
            return await handler.Handle(new AddCartItemCommand(CartActor.Create(null, "guest-race"), setup.ProductId, 1), CancellationToken.None);
        });

        var secondTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new AddCartItemCommandHandler(context, NullLogger<AddCartItemCommandHandler>.Instance);
            return await handler.Handle(new AddCartItemCommand(CartActor.Create(null, "guest-race"), setup.ProductId, 1), CancellationToken.None);
        });

        await Task.WhenAll(firstTask, secondTask);

        await using var verificationContext = database.CreateContext();
        verificationContext.Carts.Should().ContainSingle(x => x.GuestId == "guest-race");
        verificationContext.CartItems.Should().ContainSingle();
        verificationContext.CartItems.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AddCartItem_DoesNotLoseItems_WhenTwoDifferentProductsRaceForSameGuest()
    {
        using var database = TestDbContextFactory.CreateSqlite();
        var setup = await SeedCatalogAsync(database, includeSecondProduct: true);

        var firstTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new AddCartItemCommandHandler(context, NullLogger<AddCartItemCommandHandler>.Instance);
            return await handler.Handle(new AddCartItemCommand(CartActor.Create(null, "guest-race-2"), setup.ProductId, 1), CancellationToken.None);
        });

        var secondTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new AddCartItemCommandHandler(context, NullLogger<AddCartItemCommandHandler>.Instance);
            return await handler.Handle(new AddCartItemCommand(CartActor.Create(null, "guest-race-2"), setup.SecondProductId!.Value, 1), CancellationToken.None);
        });

        await Task.WhenAll(firstTask, secondTask);

        await using var verificationContext = database.CreateContext();
        verificationContext.Carts.Should().ContainSingle(x => x.GuestId == "guest-race-2");
        verificationContext.CartItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_RetriesAfterConcurrentWrite()
    {
        using var database = TestDbContextFactory.CreateSqlite();
        var setup = await SeedCartAsync(database, "guest-update");

        var firstTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new UpdateCartItemQuantityCommandHandler(context, NullLogger<UpdateCartItemQuantityCommandHandler>.Instance);
            return await handler.Handle(new UpdateCartItemQuantityCommand(CartActor.Create(null, "guest-update"), setup.CartItemId, 3), CancellationToken.None);
        });

        var secondTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new UpdateCartItemQuantityCommandHandler(context, NullLogger<UpdateCartItemQuantityCommandHandler>.Instance);
            return await handler.Handle(new UpdateCartItemQuantityCommand(CartActor.Create(null, "guest-update"), setup.CartItemId, 5), CancellationToken.None);
        });

        await Task.WhenAll(firstTask, secondTask);

        await using var verificationContext = database.CreateContext();
        verificationContext.CartItems.Should().ContainSingle();
        verificationContext.CartItems.Single().Quantity.Should().BeOneOf(3, 5);
    }

    [Fact]
    public async Task RemoveCartItem_And_ClearCart_DoNotLeaveCorruptedState_WhenTheyRace()
    {
        using var database = TestDbContextFactory.CreateSqlite();
        var setup = await SeedCartAsync(database, "guest-clear");

        var removeTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new RemoveCartItemCommandHandler(context, NullLogger<RemoveCartItemCommandHandler>.Instance);

            try
            {
                await handler.Handle(new RemoveCartItemCommand(CartActor.Create(null, "guest-clear"), setup.CartItemId), CancellationToken.None);
            }
            catch
            {
                // Either operation may win the race. The verification below checks final consistency.
            }
        });

        var clearTask = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new ClearCartCommandHandler(context, NullLogger<ClearCartCommandHandler>.Instance);
            await handler.Handle(new ClearCartCommand(CartActor.Create(null, "guest-clear")), CancellationToken.None);
        });

        await Task.WhenAll(removeTask, clearTask);

        await using var verificationContext = database.CreateContext();
        verificationContext.Carts.Should().BeEmpty();
        verificationContext.CartItems.Should().BeEmpty();
    }

    private static async Task<(Guid ProductId, Guid? SecondProductId)> SeedCatalogAsync(SqliteTestDatabase database, bool includeSecondProduct = false)
    {
        await using var context = database.CreateContext();

        var category = new Category("milk-ar", "Milk", null, null, 1);
        var brand = new Brand("brand-ar", "Almarai", "almarai.png");
        var unit = new UnitOfMeasure("liter-ar", "Liter", "L");
        context.Categories.Add(category);
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var firstProduct = new MasterProduct("milk-ar", "Full Cream Milk 1L", "milk-race", category.Id, brand.Id, unit.Id);
        firstProduct.Publish();
        context.MasterProducts.Add(firstProduct);

        MasterProduct? secondProduct = null;
        if (includeSecondProduct)
        {
            secondProduct = new MasterProduct("juice-ar", "Orange Juice 1L", "juice-race", category.Id, brand.Id, unit.Id);
            secondProduct.Publish();
            context.MasterProducts.Add(secondProduct);
        }

        await context.SaveChangesAsync();

        var vendorUser = new User("Race Vendor User", $"{Guid.NewGuid():N}@example.com", "01000000001", UserRole.Vendor);
        context.Users.Add(vendorUser);
        await context.SaveChangesAsync();

        var vendor = CreateActiveVendor(vendorUser.Id, "Race Vendor");
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.Add(new VendorProduct(vendor.Id, firstProduct.Id, 50m, 10, 60m));
        if (secondProduct is not null)
        {
            context.VendorProducts.Add(new VendorProduct(vendor.Id, secondProduct.Id, 60m, 10, 70m));
        }

        await context.SaveChangesAsync();
        return (firstProduct.Id, secondProduct?.Id);
    }

    private static async Task<(Guid CartId, Guid CartItemId, Guid ProductId)> SeedCartAsync(SqliteTestDatabase database, string guestId)
    {
        var catalog = await SeedCatalogAsync(database);

        await using var context = database.CreateContext();
        var cart = new Cart(null, guestId);
        var item = new CartItem(cart.Id, catalog.ProductId, "Full Cream Milk 1L", 2);
        cart.Items.Add(item);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        return (cart.Id, item.Id, catalog.ProductId);
    }

    private static Vendor CreateActiveVendor(Guid userId, string businessNameEn)
    {
        var vendor = new Vendor(
            userId,
            "vendor-ar",
            businessNameEn,
            "groceries",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "01000000001");

        vendor.Approve(10m, Guid.NewGuid());
        return vendor;
    }
}
