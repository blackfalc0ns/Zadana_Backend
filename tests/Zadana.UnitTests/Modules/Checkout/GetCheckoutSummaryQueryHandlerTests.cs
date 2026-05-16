using FluentAssertions;
using Moq;
using System.Reflection;
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
        result.DeliveryCheck.Status.Should().Be("deliverable");
        result.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();
        result.DeliveryQuote.TotalFee.Should().Be(7m);
        deliveryPricing.Verify(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBranchHasReliableHistoricalEta_UsesBranchCalibrationInsteadOfVendorWideAverage()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("ETA Customer", "checkout.eta@test.com", "01000000084", UserRole.Customer);
        var vendorUser = new User("ETA Vendor", "checkout.eta.vendor@test.com", "01000000085", UserRole.Vendor);
        var category = new Category("تصنيف", "Category");
        var product = new MasterProduct("منتج", "Product", "checkout-eta-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر ETA",
            "ETA Store",
            "Groceries",
            "CR-CHECKOUT-ETA",
            "checkout.eta.vendor@test.com",
            "01000000085");
        vendor.Approve(10m, Guid.NewGuid());

        var fastBranch = new VendorBranch(vendor.Id, "Fast Branch", "Fast Address", 30.0444m, 31.2357m, "01000000086", 10m);
        var slowBranch = new VendorBranch(vendor.Id, "Slow Branch", "Slow Address", 30.0500m, 31.2400m, "01000000087", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var address = new CustomerAddress(customer.Id, "ETA Customer", "01000000084", "Address", AddressLabel.Home, city: "Cairo", area: "Nasr City");
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(50m, 0m);

        context.Users.AddRange(customer, vendorUser);
        context.Categories.Add(category);
        context.MasterProducts.Add(product);
        context.Vendors.Add(vendor);
        context.VendorBranches.AddRange(fastBranch, slowBranch);
        context.VendorProducts.Add(vendorProduct);
        context.CustomerAddresses.Add(address);
        context.Carts.Add(cart);

        for (var index = 0; index < 6; index++)
        {
            context.Orders.Add(CreateDeliveredOrder(customer.Id, vendor.Id, fastBranch.Id, $"FAST-{index}", 42 + index));
            context.Orders.Add(CreateDeliveredOrder(customer.Id, vendor.Id, slowBranch.Id, $"SLOW-{index}", 92 + index));
        }

        await context.SaveChangesAsync();

        var paymobGateway = new Mock<Zadana.Application.Modules.Payments.Interfaces.IPaymobGateway>();
        paymobGateway.SetupGet(gateway => gateway.IsEnabled).Returns(true);

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(fastBranch.Id, address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, paymobGateway.Object, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.EstimatedDeliveryWindow.Source.Should().Be("hybrid_operational");
        result.EstimatedDeliveryWindow.Confidence.Should().Be("medium");
        result.EstimatedDeliveryWindow.MaxMinutes.Should().BeLessThan(70);
    }

    [Fact]
    public async Task Handle_WhenAddressIsInSameCity_DoesNotRejectDeliveryBecauseOfRadius()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("City Customer", "checkout.city@test.com", "01000000088", UserRole.Customer);
        var vendorUser = new User("City Vendor", "checkout.city.vendor@test.com", "01000000089", UserRole.Vendor);
        var category = new Category("تصنيف", "Category");
        var product = new MasterProduct("منتج", "Product", "checkout-city-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر المدينة",
            "City Store",
            "Groceries",
            "CR-CHECKOUT-CITY",
            "checkout.city.vendor@test.com",
            "01000000089",
            city: "الدمام");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(vendor.Id, "Main Branch", "Branch Address", 26.4207m, 50.0888m, "01000000090", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var address = new CustomerAddress(
            customer.Id,
            "City Customer",
            "01000000088",
            "الدمام - المنطقة الشرقية",
            AddressLabel.Home,
            city: "الدمام",
            area: "حي",
            latitude: 26.30m,
            longitude: 50.20m);
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
            .ReturnsAsync(new DeliveryPriceQuote(15m, 16.7m, 0m, 31.7m, 20.33m, "zone", "Zone rule", 1m, 20.33m, 3m, 28.7m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, paymobGateway.Object, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.DeliveryCheck.Status.Should().Be("deliverable");
        result.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();
    }

    private static Order CreateDeliveredOrder(Guid userId, Guid vendorId, Guid vendorBranchId, string orderNumber, int totalMinutes)
    {
        var order = new Order(
            orderNumber,
            userId,
            vendorId,
            Guid.NewGuid(),
            Zadana.Domain.Modules.Payments.Enums.PaymentMethodType.CashOnDelivery,
            100m,
            0m,
            10m,
            10m,
            0m,
            0m,
            null,
            null,
            null,
            1.2m,
            2.5m,
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
            5m,
            vendorBranchId: vendorBranchId);

        var placedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(-totalMinutes);
        var acceptedAt = placedAt.AddMinutes(4);
        var preparingAt = placedAt.AddMinutes(6);
        var readyAt = placedAt.AddMinutes(22);
        var driverAssignedAt = placedAt.AddMinutes(28);
        var pickedUpAt = placedAt.AddMinutes(34);
        var deliveredAt = placedAt.AddMinutes(totalMinutes);

        order.ChangeStatus(OrderStatus.Accepted);
        order.ChangeStatus(OrderStatus.Preparing);
        order.ChangeStatus(OrderStatus.ReadyForPickup);
        order.ChangeStatus(OrderStatus.DriverAssigned);
        order.ChangeStatus(OrderStatus.PickedUp);
        order.ChangeStatus(OrderStatus.Delivered);

        SetPrivateProperty(order, nameof(Order.PlacedAtUtc), placedAt);
        SetPrivateProperty(order, nameof(Order.DeliveredAtUtc), deliveredAt);

        var historyTimes = new Dictionary<OrderStatus, DateTime>
        {
            [OrderStatus.Accepted] = acceptedAt,
            [OrderStatus.Preparing] = preparingAt,
            [OrderStatus.ReadyForPickup] = readyAt,
            [OrderStatus.DriverAssigned] = driverAssignedAt,
            [OrderStatus.PickedUp] = pickedUpAt,
            [OrderStatus.Delivered] = deliveredAt
        };

        foreach (var historyItem in order.StatusHistory)
        {
            if (historyTimes.TryGetValue(historyItem.NewStatus, out var historyTime))
            {
                SetPrivateProperty(historyItem, nameof(historyItem.CreatedAtUtc), historyTime);
            }
        }

        return order;
    }

    private static void SetPrivateProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull($"property {propertyName} should exist on {target.GetType().Name}");
        property!.SetValue(target, value);
    }
}
