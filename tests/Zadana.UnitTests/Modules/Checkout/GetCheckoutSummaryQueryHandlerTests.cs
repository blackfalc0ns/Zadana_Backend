using FluentAssertions;
using Moq;
using Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Checkout;

public class GetCheckoutSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenVendorProductsHaveNoBranchButVendorHasSingleActiveBranch_UsesThatBranchForDeliveryQuote()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("Checkout Customer", "checkout.summary@test.com", "01000000081", UserRole.Customer);
        var vendorUser = new User("Checkout Vendor", "checkout.summary.vendor@test.com", "01000000082", UserRole.Vendor);
        var category = new Category("تصنيف", "Category");
        var product = new MasterProduct("منتج", "Product", "checkout-summary-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر",
            "Store",
            "Groceries",
            "CR-CHECKOUT-1",
            "checkout.summary.vendor@test.com",
            "01000000082");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(vendor.Id, "Main Branch", "Branch Address", 30.0444m, 31.2357m, "01000000083", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var address = new CustomerAddress(customer.Id, "Checkout Customer", "01000000081", "Address", AddressLabel.Home, city: "Cairo", area: "Nasr City");
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(50m, 0m);

        context.Users.AddRange(customer, vendorUser);
        context.Categories.Add(category);
        context.MasterProducts.Add(product);
        context.Vendors.Add(vendor);
        context.VendorBranches.Add(branch);
        context.VendorProducts.Add(vendorProduct);
        context.CustomerAddresses.Add(address);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        var paymobGateway = new Mock<Zadana.Application.Modules.Payments.Interfaces.IPaymobGateway>();
        paymobGateway.SetupGet(gateway => gateway.IsEnabled).Returns(true);

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, paymobGateway.Object, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.SelectedAddress.Should().NotBeNull();
        result.DeliveryQuote.TotalFee.Should().Be(7m);
        deliveryPricing.Verify(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
