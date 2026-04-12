using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.Commands;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Favorites;

public class FavoriteCommandsTests
{
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();

    public FavoriteCommandsTests()
    {
        _localizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key switch
            {
                "FavoriteAddedSuccessfully" => "Product added to favorites successfully.",
                "FavoriteRemovedSuccessfully" => "Product removed from favorites successfully.",
                "FavoritesClearedSuccessfully" => "Favorites cleared successfully.",
                "UserNotAuthenticated" => "User is not authenticated.",
                _ => key
            }));
    }

    [Fact]
    public async Task AddFavorite_ReturnsLocalizedSuccessMessage()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedProductAsync(context);
        var handler = new AddFavoriteCommandHandler(context, _localizerMock.Object);

        var result = await handler.Handle(new AddFavoriteCommand(setup.UserId, null, setup.Product.Id), CancellationToken.None);

        result.Message.Should().Be("Product added to favorites successfully.");
        result.Item.Should().NotBeNull();
        result.Summary.ItemsCount.Should().Be(1);
    }

    [Fact]
    public async Task RemoveFavorite_ReturnsLocalizedSuccessMessage()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedProductAsync(context);
        var addHandler = new AddFavoriteCommandHandler(context, _localizerMock.Object);
        await addHandler.Handle(new AddFavoriteCommand(setup.UserId, null, setup.Product.Id), CancellationToken.None);

        var handler = new RemoveFavoriteCommandHandler(context, _localizerMock.Object);
        var result = await handler.Handle(new RemoveFavoriteCommand(setup.UserId, null, setup.Product.Id), CancellationToken.None);

        result.Message.Should().Be("Product removed from favorites successfully.");
        result.Summary.ItemsCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearFavorites_ReturnsLocalizedSuccessMessage()
    {
        await using var context = TestDbContextFactory.Create();
        var setup = await SeedProductAsync(context);
        var addHandler = new AddFavoriteCommandHandler(context, _localizerMock.Object);
        await addHandler.Handle(new AddFavoriteCommand(setup.UserId, null, setup.Product.Id), CancellationToken.None);

        var handler = new ClearFavoritesCommandHandler(context, _localizerMock.Object);
        var result = await handler.Handle(new ClearFavoritesCommand(setup.UserId, null), CancellationToken.None);

        result.Message.Should().Be("Favorites cleared successfully.");
    }

    private static async Task<ProductSetup> SeedProductAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var category = new Category("milk-ar", "Milk", null, null, 1);
        var brand = new Brand("brand-ar", "Almarai", "almarai.png");
        var unit = new UnitOfMeasure("liter-ar", "Liter", "L");
        context.Categories.Add(category);
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var product = new MasterProduct("milk-ar", "Full Cream Milk 1L", "milk", category.Id, brand.Id, unit.Id);
        product.Publish();
        product.AddImage("https://cdn.test/milk-primary.jpg", displayOrder: 0, isPrimary: true);
        context.MasterProducts.Add(product);

        var vendor = new Vendor(
            Guid.NewGuid(),
            "vendor-ar",
            "Green Valley Market",
            "groceries",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "01000000001");
        vendor.Approve(10m, Guid.NewGuid());
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        context.VendorProducts.Add(new VendorProduct(vendor.Id, product.Id, 50m, 10, 60m));
        await context.SaveChangesAsync();

        return new ProductSetup(Guid.NewGuid(), product);
    }

    private sealed record ProductSetup(Guid UserId, MasterProduct Product);
}
