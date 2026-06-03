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
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;
using Zadana.UnitTests.TestHelpers;

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

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.SelectedAddress.Should().NotBeNull();
        result.DeliveryCheck.Status.Should().Be("deliverable");
        result.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();
        result.DeliveryQuote.TotalFee.Should().Be(7m);
        deliveryPricing.Verify(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVendorProductsHaveNoBranchAndVendorHasMultipleBranches_UsesNearestActiveBranchForDeliveryQuote()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("Nearest Branch Customer", "checkout.nearest@test.com", "01000000181", UserRole.Customer);
        var vendorUser = new User("Nearest Branch Vendor", "checkout.nearest.vendor@test.com", "01000000182", UserRole.Vendor);
        var category = new Category("Category", "Category");
        var product = new MasterProduct("Product", "Product", "checkout-nearest-branch-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "Nearest Store",
            "Nearest Store",
            "Groceries",
            "CR-CHECKOUT-NEAREST",
            "checkout.nearest.vendor@test.com",
            "01000000182");
        vendor.Approve(10m, Guid.NewGuid());

        var nearBranch = new VendorBranch(vendor.Id, "Dammam Branch", "Dammam Address", 26.4207m, 50.1033m, "01000000183", 12m);
        var farBranch = new VendorBranch(vendor.Id, "Khobar Branch", "Khobar Address", 26.2828m, 50.2088m, "01000000184", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var address = new CustomerAddress(
            customer.Id,
            "Nearest Branch Customer",
            "01000000181",
            "Dhahran Address",
            AddressLabel.Home,
            city: "Dhahran",
            area: "Eastern Province",
            latitude: 26.4207101m,
            longitude: 50.0887807m);
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(50m, 0m);

        context.Users.AddRange(customer, vendorUser);
        context.Categories.Add(category);
        context.MasterProducts.Add(product);
        context.Vendors.Add(vendor);
        context.VendorBranches.AddRange(nearBranch, farBranch);
        context.VendorProducts.Add(vendorProduct);
        context.CustomerAddresses.Add(address);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(nearBranch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Nearest branch", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.DeliveryCheck.Status.Should().Be("deliverable");
        result.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();
        result.DeliveryQuote.TotalFee.Should().Be(7m);
        deliveryPricing.Verify(service => service.QuoteAsync(nearBranch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Once);
        deliveryPricing.Verify(service => service.QuoteAsync(farBranch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Never);
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

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(fastBranch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.EstimatedDeliveryWindow.Source.Should().NotBe("distance_baseline");
        result.EstimatedDeliveryWindow.Confidence.Should().Be("medium");
        result.EstimatedDeliveryWindow.MaxMinutes.Should().BeGreaterThan(result.EstimatedDeliveryWindow.MinMinutes);
        result.EstimatedDeliveryWindow.MaxMinutes.Should().BeLessThan(120);
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

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(15m, 16.7m, 0m, 31.7m, 20.33m, "zone", "Zone rule", 1m, 20.33m, 3m, 28.7m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.DeliveryCheck.Status.Should().Be("deliverable");
        result.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenVendorHasNoHistory_UsesRegionalFallbackBeforeDistanceBaseline()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("Regional Customer", "checkout.regional@test.com", "01000000094", UserRole.Customer);
        var vendorUser = new User("Regional Vendor", "checkout.regional.vendor@test.com", "01000000095", UserRole.Vendor);
        var historicalVendorUser = new User("History Vendor", "checkout.regional.history@test.com", "01000000096", UserRole.Vendor);
        var category = new Category("تصنيف", "Category");
        var product = new MasterProduct("منتج", "Product", "checkout-regional-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر جديد",
            "New Store",
            "Groceries",
            "CR-CHECKOUT-REGIONAL",
            "checkout.regional.vendor@test.com",
            "01000000095");
        vendor.Approve(10m, Guid.NewGuid());

        var historicalVendor = new Vendor(
            historicalVendorUser.Id,
            "متجر تاريخي",
            "History Store",
            "Groceries",
            "CR-CHECKOUT-REGIONAL-HISTORY",
            "checkout.regional.history@test.com",
            "01000000096");
        historicalVendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(vendor.Id, "Main Branch", "Branch Address", 30.0444m, 31.2357m, "01000000097", 10m);
        var historyBranch = new VendorBranch(historicalVendor.Id, "History Branch", "History Address", 30.0500m, 31.2400m, "01000000098", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var historyAddress = new CustomerAddress(customer.Id, "History Customer", "01000000099", "History Address", AddressLabel.Home, city: "Cairo", area: "Nasr City");
        var address = new CustomerAddress(customer.Id, "Regional Customer", "01000000094", "Address", AddressLabel.Home, city: "Cairo", area: "Nasr City");
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(50m, 0m);

        context.Users.AddRange(customer, vendorUser, historicalVendorUser);
        context.Categories.Add(category);
        context.MasterProducts.Add(product);
        context.Vendors.AddRange(vendor, historicalVendor);
        context.VendorBranches.AddRange(branch, historyBranch);
        context.VendorProducts.Add(vendorProduct);
        context.CustomerAddresses.AddRange(address, historyAddress);
        context.Carts.Add(cart);

        for (var index = 0; index < 5; index++)
        {
            var regionalOrder = CreateDeliveredOrder(customer.Id, historicalVendor.Id, historyBranch.Id, historyAddress.Id, $"REG-{index}", 52 + index);
            context.Orders.Add(regionalOrder);
        }

        await context.SaveChangesAsync();

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.EstimatedDeliveryWindow.Source.Should().Be("regional_fallback");
        result.EstimatedDeliveryWindow.Confidence.Should().NotBe("low");
        result.EstimatedDeliveryWindow.MaxMinutes.Should().BeGreaterThan(result.EstimatedDeliveryWindow.MinMinutes);
    }

    [Fact]
    public async Task Handle_WhenVendorIsManuallyOffline_ThrowsBusinessRuleException()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("Offline Customer", "checkout.offline@test.com", "01000000100", UserRole.Customer);
        var vendorUser = new User("Offline Vendor", "checkout.offline.vendor@test.com", "01000000101", UserRole.Vendor);
        var category = new Category("تصنيف", "Category");
        var product = new MasterProduct("منتج", "Product", "checkout-offline-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر مغلق",
            "Closed Store",
            "Groceries",
            "CR-CHECKOUT-OFFLINE",
            "checkout.offline.vendor@test.com",
            "01000000101");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(vendor.Id, "Main Branch", "Branch Address", 30.0444m, 31.2357m, "01000000102", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var address = new CustomerAddress(customer.Id, "Offline Customer", "01000000100", "Address", AddressLabel.Home, city: "Cairo", area: "Nasr City");
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
        context.VendorWorkspaceStates.Add(new VendorWorkspaceState(vendor.Id, "store-availability", "{\"manual_mode\":\"offline\"}"));
        await context.SaveChangesAsync();

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var act = () => handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "VENDOR_OFFLINE");
    }

    [Fact]
    public async Task Handle_WhenVendorCityIsEnglishAndAddressCityIsArabic_TreatsThemAsSameCity()
    {
        await using var context = TestDbContextFactory.Create();

        var customer = new User("Cross Language Customer", "checkout.city.alias@test.com", "01000000091", UserRole.Customer);
        var vendorUser = new User("Cross Language Vendor", "checkout.city.alias.vendor@test.com", "01000000092", UserRole.Vendor);
        var category = new Category("تصنيف", "Category");
        var product = new MasterProduct("منتج", "Product", "checkout-city-alias-product", category.Id);
        product.Publish();

        var vendor = new Vendor(
            vendorUser.Id,
            "متجر الدمام",
            "Dammam Store",
            "Groceries",
            "CR-CHECKOUT-CITY-ALIAS",
            "checkout.city.alias.vendor@test.com",
            "01000000092",
            city: "DAMMAM");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(vendor.Id, "Main Branch", "Branch Address", 26.4207m, 50.0888m, "01000000093", 10m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 50m, 10);
        var address = new CustomerAddress(
            customer.Id,
            "Cross Language Customer",
            "01000000091",
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

        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(15m, 16.7m, 0m, 31.7m, 20.33m, "zone", "Zone rule", 1m, 20.33m, 3m, 28.7m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);

        var result = await handler.Handle(
            new GetCheckoutSummaryQuery(customer.Id, vendor.Id, address.Id, null, "cash"),
            CancellationToken.None);

        result.DeliveryCheck.Status.Should().Be("deliverable");
        result.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();
    }

    private static Order CreateDeliveredOrder(Guid userId, Guid vendorId, Guid vendorBranchId, string orderNumber, int totalMinutes) =>
        CreateDeliveredOrder(userId, vendorId, vendorBranchId, Guid.NewGuid(), orderNumber, totalMinutes);

    private static Order CreateDeliveredOrder(Guid userId, Guid vendorId, Guid vendorBranchId, Guid customerAddressId, string orderNumber, int totalMinutes)
    {
        var order = new Order(
            orderNumber,
            userId,
            vendorId,
            customerAddressId,
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


