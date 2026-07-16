using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Api.Security;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Application.Tests.Integration;

public class CartEndpoints_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private const string GuestHeaderName = "X-Device-Id";
    private const string GuestSignatureHeaderName = "X-Device-Signature";

    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public CartEndpoints_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCart_WithCamelCaseVendorId_WhenSelectedVendorMissesOneProduct_ShouldKeepPricedTotal()
    {
        var scenario = await SeedCartScenarioAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/cart?vendorId={scenario.SelectedVendorId}");
        request.Headers.Add(GuestHeaderName, scenario.GuestId);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = await ReadJsonAsync(response);

        var summary = payload.RootElement.GetProperty("summary");
        ReadDecimal(summary, "totalAmount").Should().Be(140m);
        ReadDecimal(summary, "subtotal").Should().Be(150m);
        ReadDecimal(summary, "discountAmount").Should().Be(10m);
        summary.GetProperty("canCheckout").GetBoolean().Should().BeTrue();
        summary.GetProperty("hasUnavailableItems").GetBoolean().Should().BeTrue();
        summary.GetProperty("unavailableItemsCount").GetInt32().Should().Be(1);
        summary.GetProperty("requiresUnavailableItemsConfirmation").GetBoolean().Should().BeTrue();

        var unavailableItem = payload.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == scenario.UnavailableItemId);
        unavailableItem.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItem_WithCamelCaseVendorId_WhenRemovingUnavailableItem_ShouldPreserveSelectedVendorTotal()
    {
        var scenario = await SeedCartScenarioAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/cart/items/{scenario.UnavailableItemId}?vendorId={scenario.SelectedVendorId}");
        request.Headers.Add(GuestHeaderName, scenario.GuestId);
        request.Headers.Add(GuestSignatureHeaderName, scenario.GuestSignature);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = await ReadJsonAsync(response);

        var summary = payload.RootElement.GetProperty("summary");
        summary.GetProperty("itemsCount").GetInt32().Should().Be(2);
        summary.GetProperty("totalQuantity").GetInt32().Should().Be(3);
        ReadDecimal(summary, "subtotal").Should().Be(150m);
        ReadDecimal(summary, "discountAmount").Should().Be(10m);
        ReadDecimal(summary, "totalAmount").Should().Be(140m);
        summary.GetProperty("canCheckout").GetBoolean().Should().BeTrue();
        summary.GetProperty("hasUnavailableItems").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItem_WithCamelCaseVendorId_WhenRemovingAvailableItem_ShouldRecalculateTotalWithoutDroppingToZero()
    {
        var scenario = await SeedCartScenarioAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/cart/items/{scenario.FirstAvailableItemId}?vendorId={scenario.SelectedVendorId}");
        request.Headers.Add(GuestHeaderName, scenario.GuestId);
        request.Headers.Add(GuestSignatureHeaderName, scenario.GuestSignature);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = await ReadJsonAsync(response);

        var summary = payload.RootElement.GetProperty("summary");
        summary.GetProperty("itemsCount").GetInt32().Should().Be(2);
        summary.GetProperty("totalQuantity").GetInt32().Should().Be(3);
        ReadDecimal(summary, "subtotal").Should().Be(90m);
        ReadDecimal(summary, "discountAmount").Should().Be(0m);
        ReadDecimal(summary, "totalAmount").Should().Be(90m);
        summary.GetProperty("canCheckout").GetBoolean().Should().BeTrue();
        summary.GetProperty("hasUnavailableItems").GetBoolean().Should().BeTrue();
        summary.GetProperty("unavailableItemsCount").GetInt32().Should().Be(1);
        summary.GetProperty("requiresUnavailableItemsConfirmation").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task RemoveItem_WithoutVendorId_WhenNoSingleVendorCoversCart_ShouldReturnBestPartialTotal()
    {
        var scenario = await SeedCartScenarioAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/cart/items/{scenario.FirstAvailableItemId}");
        request.Headers.Add(GuestHeaderName, scenario.GuestId);
        request.Headers.Add(GuestSignatureHeaderName, scenario.GuestSignature);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = await ReadJsonAsync(response);

        var summary = payload.RootElement.GetProperty("summary");
        summary.GetProperty("itemsCount").GetInt32().Should().Be(2);
        summary.GetProperty("totalQuantity").GetInt32().Should().Be(3);
        ReadDecimal(summary, "subtotal").Should().Be(90m);
        ReadDecimal(summary, "discountAmount").Should().Be(0m);
        ReadDecimal(summary, "totalAmount").Should().Be(90m);
        summary.GetProperty("isPricingAvailable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateItem_WithCamelCaseVendorId_ShouldRecalculateSummaryForSelectedVendor()
    {
        var scenario = await SeedCartScenarioAsync();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/cart/items/{scenario.SecondAvailableItemId}?vendorId={scenario.SelectedVendorId}")
        {
            Content = JsonContent.Create(new { quantity = 3 })
        };
        request.Headers.Add(GuestHeaderName, scenario.GuestId);
        request.Headers.Add(GuestSignatureHeaderName, scenario.GuestSignature);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("item").GetProperty("quantity").GetInt32().Should().Be(3);
        var summary = payload.RootElement.GetProperty("summary");
        ReadDecimal(summary, "subtotal").Should().Be(195m);
        ReadDecimal(summary, "discountAmount").Should().Be(10m);
        ReadDecimal(summary, "totalAmount").Should().Be(185m);
        summary.GetProperty("hasUnavailableItems").GetBoolean().Should().BeTrue();
    }

    private async Task<CartEndpointScenario> SeedCartScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = new Category($"cat-ar-{Guid.NewGuid():N}", $"Cart Category {Guid.NewGuid():N}", null, null, 1);
        var brand = new Brand($"brand-ar-{Guid.NewGuid():N}", $"Cart Brand {Guid.NewGuid():N}", "brand.png");
        var unit = new UnitOfMeasure($"unit-ar-{Guid.NewGuid():N}", $"Piece {Guid.NewGuid():N}", "pc");
        db.Categories.Add(category);
        db.Brands.Add(brand);
        db.UnitsOfMeasure.Add(unit);
        await db.SaveChangesAsync();

        var availableProductOne = CreatePublishedProduct("Available One", category.Id, brand.Id, unit.Id);
        var unavailableAtSelectedProduct = CreatePublishedProduct("Unavailable At Selected", category.Id, brand.Id, unit.Id);
        var availableProductTwo = CreatePublishedProduct("Available Two", category.Id, brand.Id, unit.Id);
        db.MasterProducts.AddRange(availableProductOne, unavailableAtSelectedProduct, availableProductTwo);

        var selectedVendor = CreateActiveVendor("Selected Store");
        var otherVendor = CreateActiveVendor("Other Store");
        db.Vendors.AddRange(selectedVendor, otherVendor);
        await db.SaveChangesAsync();

        db.VendorProducts.AddRange(
            new VendorProduct(selectedVendor.Id, availableProductOne.Id, 50m, 10, 60m),
            new VendorProduct(selectedVendor.Id, availableProductTwo.Id, 45m, 10),
            new VendorProduct(otherVendor.Id, unavailableAtSelectedProduct.Id, 30m, 10));

        var guestId = $"cart-endpoint-{Guid.NewGuid():N}";
        var cart = new Cart(null, guestId);
        var firstAvailableItem = new CartItem(cart.Id, availableProductOne.Id, availableProductOne.NameEn, 1);
        var unavailableItem = new CartItem(cart.Id, unavailableAtSelectedProduct.Id, unavailableAtSelectedProduct.NameEn, 1);
        var secondAvailableItem = new CartItem(cart.Id, availableProductTwo.Id, availableProductTwo.NameEn, 2);
        cart.Items.Add(firstAvailableItem);
        cart.Items.Add(unavailableItem);
        cart.Items.Add(secondAvailableItem);

        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var signer = _factory.Services.GetRequiredService<GuestCartSigner>();

        return new CartEndpointScenario(
            guestId,
            signer.Sign(guestId),
            selectedVendor.Id,
            firstAvailableItem.Id,
            unavailableItem.Id,
            secondAvailableItem.Id);
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private static decimal? ReadDecimal(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.GetDecimal();
    }

    private sealed record CartEndpointScenario(
        string GuestId,
        string GuestSignature,
        Guid SelectedVendorId,
        Guid FirstAvailableItemId,
        Guid UnavailableItemId,
        Guid SecondAvailableItemId);
}
