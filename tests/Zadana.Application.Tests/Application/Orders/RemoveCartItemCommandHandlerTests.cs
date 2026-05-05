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

public class RemoveCartItemCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithoutVendorId_WhenSingleVendorCanPriceRemainingItems_ShouldReturnUpdatedTotals()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid();
        var vendor = new Vendor(
            Guid.NewGuid(),
            "متجر واحد",
            "Single Store",
            "grocery",
            "CR-100",
            "store@example.com",
            "+201000000001");
        vendor.Approve(10m, Guid.NewGuid());

        var removedProduct = new MasterProduct("حليب", "Milk", "milk", Guid.NewGuid());
        removedProduct.Publish();

        var remainingProduct = new MasterProduct("خبز", "Bread", "bread", Guid.NewGuid());
        remainingProduct.Publish();

        var removedOffer = new VendorProduct(vendor.Id, removedProduct.Id, 30m, stockQuantity: 5);
        var remainingOffer = new VendorProduct(vendor.Id, remainingProduct.Id, 50m, stockQuantity: 5);

        var cart = new Cart(userId);
        var removedItem = new CartItem(cart.Id, removedProduct.Id, removedProduct.NameEn, 1);
        var remainingItem = new CartItem(cart.Id, remainingProduct.Id, remainingProduct.NameEn, 2);
        cart.Items.Add(removedItem);
        cart.Items.Add(remainingItem);

        dbContext.Vendors.Add(vendor);
        dbContext.MasterProducts.AddRange(removedProduct, remainingProduct);
        dbContext.VendorProducts.AddRange(removedOffer, remainingOffer);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var handler = new RemoveCartItemCommandHandler(
            dbContext,
            NullLogger<RemoveCartItemCommandHandler>.Instance);

        var result = await handler.Handle(
            new RemoveCartItemCommand(CartActor.Create(userId, null), removedItem.Id),
            CancellationToken.None);

        result.Summary.ItemsCount.Should().Be(1);
        result.Summary.TotalQuantity.Should().Be(2);
        result.Summary.Subtotal.Should().Be(100m);
        result.Summary.DiscountAmount.Should().Be(0m);
        result.Summary.TotalAmount.Should().Be(100m);
        result.Summary.IsPricingAvailable.Should().BeTrue();
        result.Summary.CanCheckout.Should().BeTrue();
        result.Summary.HasUnavailableItems.Should().BeFalse();
        dbContext.CartItems.Should().ContainSingle(item => item.Id == remainingItem.Id);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
