using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zadana.Application.Modules.Orders.Commands.RemoveCartItem;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class RemoveUnavailableCartItemCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithVendorId_WhenRemovingUnavailableItem_ShouldKeepPricedTotalsForRemainingItems()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid();
        var category = new Category($"cat-ar-{Guid.NewGuid():N}", $"Cart Category {Guid.NewGuid():N}", null, null, 1);
        var brand = new Brand($"brand-ar-{Guid.NewGuid():N}", $"Cart Brand {Guid.NewGuid():N}", "brand.png");
        var unit = new UnitOfMeasure($"unit-ar-{Guid.NewGuid():N}", $"Piece {Guid.NewGuid():N}", "pc");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        dbContext.UnitsOfMeasure.Add(unit);
        await dbContext.SaveChangesAsync();

        var availableProductOne = CreatePublishedProduct("Available One", category.Id, brand.Id, unit.Id);
        var unavailableAtSelectedProduct = CreatePublishedProduct("Unavailable At Selected", category.Id, brand.Id, unit.Id);
        var availableProductTwo = CreatePublishedProduct("Available Two", category.Id, brand.Id, unit.Id);
        dbContext.MasterProducts.AddRange(availableProductOne, unavailableAtSelectedProduct, availableProductTwo);

        var selectedVendor = CreateActiveVendor("Selected Store");
        var otherVendor = CreateActiveVendor("Other Store");
        dbContext.Vendors.AddRange(selectedVendor, otherVendor);
        await dbContext.SaveChangesAsync();

        dbContext.VendorProducts.AddRange(
            new VendorProduct(selectedVendor.Id, availableProductOne.Id, 50m, 10, 60m),
            new VendorProduct(selectedVendor.Id, availableProductTwo.Id, 45m, 10),
            new VendorProduct(otherVendor.Id, unavailableAtSelectedProduct.Id, 30m, 10));

        var cart = new Cart(userId);
        var firstAvailableItem = new CartItem(cart.Id, availableProductOne.Id, availableProductOne.NameEn, 1);
        var unavailableItem = new CartItem(cart.Id, unavailableAtSelectedProduct.Id, unavailableAtSelectedProduct.NameEn, 1);
        var secondAvailableItem = new CartItem(cart.Id, availableProductTwo.Id, availableProductTwo.NameEn, 2);
        cart.Items.Add(firstAvailableItem);
        cart.Items.Add(unavailableItem);
        cart.Items.Add(secondAvailableItem);

        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var handler = new RemoveCartItemCommandHandler(
            dbContext,
            NullLogger<RemoveCartItemCommandHandler>.Instance);

        var result = await handler.Handle(
            new RemoveCartItemCommand(CartActor.Create(userId, null), unavailableItem.Id, selectedVendor.Id),
            CancellationToken.None);

        result.Summary.ItemsCount.Should().Be(2);
        result.Summary.TotalQuantity.Should().Be(3);
        result.Summary.Subtotal.Should().Be(150m);
        result.Summary.DiscountAmount.Should().Be(10m);
        result.Summary.TotalAmount.Should().Be(140m);
        result.Summary.IsPricingAvailable.Should().BeTrue();
        result.Summary.CanCheckout.Should().BeTrue();
        result.Summary.HasUnavailableItems.Should().BeFalse();

        var persistedCart = await dbContext.Carts
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleAsync(item => item.UserId == userId);

        persistedCart.Items.Should().HaveCount(2);
        persistedCart.Items.Should().NotContain(item => item.Id == unavailableItem.Id);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static MasterProduct CreatePublishedProduct(string nameEn, Guid categoryId, Guid brandId, Guid unitId)
    {
        var slug = $"{nameEn.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}";
        var product = new MasterProduct($"{slug}-ar", nameEn, slug, categoryId, brandId, unitId);
        product.Publish();
        product.AddImage($"https://cdn.test/{slug}.jpg", displayOrder: 0, isPrimary: true);
        return product;
    }

    private static Vendor CreateActiveVendor(string nameEn)
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            $"ar-{Guid.NewGuid():N}",
            nameEn,
            "groceries",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@test.com",
            "01000000001");

        vendor.Approve(10m, Guid.NewGuid());
        return vendor;
    }
}
