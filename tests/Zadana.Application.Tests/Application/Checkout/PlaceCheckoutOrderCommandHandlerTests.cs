using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Modules.Orders.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Checkout;

public class PlaceCheckoutOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCashOrderPlaced_ShouldPersistPendingVendorAcceptanceHistoryWithoutConcurrencyFailure()
    {
        await using var dbContext = CreateDbContext();

        var customer = new User("Checkout Customer", "checkout.customer@test.com", "01000000030", UserRole.Customer);
        var vendorUser = new User("Checkout Vendor", "checkout.vendor@test.com", "01000000031", UserRole.Vendor);
        var category = new Category("إلكترونيات", "Electronics");
        var product = new MasterProduct("جراب آيفون", "Transparent iPhone Case", "transparent-iphone-case-test", category.Id);
        product.Publish();

        var vendor = new Zadana.Domain.Modules.Vendors.Entities.Vendor(
            vendorUser.Id,
            "متجر الاختبار",
            "Checkout Test Store",
            "Electronics",
            "1234567890",
            "checkout.vendor@test.com",
            "01000000031");
        vendor.Approve(10m, Guid.NewGuid());
        vendor.UpdateOperationsSettings(true, null, 30);
        vendor.UpdateNotificationSettings(true, false, true);

        var branch = new Zadana.Domain.Modules.Vendors.Entities.VendorBranch(vendor.Id, "Main Branch", "Nasr City", 30.0444m, 31.2357m, "01000000032", 15m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 49m, 10, tradePrice: 35m, vendorBranchId: branch.Id);
        var address = new CustomerAddress(customer.Id, "Checkout Customer", "01000000030", "Nasr City 12", AddressLabel.Home, city: "Cairo");
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(49m, 0m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Categories.Add(category);
        dbContext.MasterProducts.Add(product);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.VendorProducts.Add(vendorProduct);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var orderRepository = new OrderRepository(dbContext);
        var placeOrderHandler = new PlaceOrderCommandHandler(orderRepository, TestLocalizer.Create<SharedResource>(), dbContext);
        var sender = new SenderProxy(type =>
        {
            if (type == typeof(IRequestHandler<PlaceOrderCommand, Guid>))
            {
                return placeOrderHandler;
            }

            throw new InvalidOperationException($"Unsupported handler: {type.FullName}");
        });

        var publisherMock = new Mock<IPublisher>();
        publisherMock
            .Setup(publisher => publisher.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisherMock
            .Setup(publisher => publisher.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var deliveryPricingMock = new Mock<IDeliveryPricingService>();
        deliveryPricingMock
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new PlaceCheckoutOrderCommandHandler(
            dbContext,
            TestPaymentGatewayResolver.Disabled(),
            deliveryPricingMock.Object,
            sender,
            dbContext,
            publisherMock.Object);

        var result = await handler.Handle(
            new PlaceCheckoutOrderCommand(
                customer.Id,
                vendor.Id,
                address.Id,
                null,
                "cash",
                null,
                "checkout regression"),
            CancellationToken.None);

        result.Order.Status.Should().Be("processing");

        var savedOrder = await dbContext.Orders
            .Include(order => order.StatusHistory)
            .SingleAsync();

        savedOrder.Status.Should().Be(OrderStatus.PendingVendorAcceptance);
        savedOrder.EtaMinMinutes.Should().NotBeNull();
        savedOrder.EtaMaxMinutes.Should().NotBeNull();
        savedOrder.EtaSource.Should().NotBeNullOrWhiteSpace();
        savedOrder.EtaCalculationMode.Should().NotBeNullOrWhiteSpace();
        savedOrder.EtaExplanation.Should().NotBeNullOrWhiteSpace();
        savedOrder.StatusHistory
            .Select(history => history.NewStatus)
            .Should()
            .ContainInOrder(OrderStatus.Placed, OrderStatus.PendingVendorAcceptance);
    }

    [Fact]
    public async Task Handle_WhenBankOrderPlaced_ShouldWaitForAutomaticBankConfirmation()
    {
        await using var dbContext = CreateDbContext();

        var customer = new User("Checkout Bank Customer", "checkout.bank.customer@test.com", "01000000230", UserRole.Customer);
        var vendorUser = new User("Checkout Bank Vendor", "checkout.bank.vendor@test.com", "01000000231", UserRole.Vendor);
        var category = new Category("قطع غيار", "Parts");
        var product = new MasterProduct("فلتر", "Filter", "filter-bank-test", category.Id);
        product.Publish();

        var vendor = new Zadana.Domain.Modules.Vendors.Entities.Vendor(
            vendorUser.Id,
            "متجر التحويل",
            "Bank Checkout Store",
            "Parts",
            "1234567892",
            "checkout.bank.vendor@test.com",
            "01000000231");
        vendor.Approve(10m, Guid.NewGuid());
        vendor.UpdateOperationsSettings(true, null, 30);

        var branch = new Zadana.Domain.Modules.Vendors.Entities.VendorBranch(vendor.Id, "Main Branch", "Nasr City", 30.0444m, 31.2357m, "01000000232", 15m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 60m, 10, tradePrice: 40m, vendorBranchId: branch.Id);
        var address = new CustomerAddress(customer.Id, "Checkout Bank Customer", "01000000230", "Nasr City 13", AddressLabel.Home, city: "Cairo");
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(60m, 0m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Categories.Add(category);
        dbContext.MasterProducts.Add(product);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.VendorProducts.Add(vendorProduct);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var orderRepository = new OrderRepository(dbContext);
        var placeOrderHandler = new PlaceOrderCommandHandler(orderRepository, TestLocalizer.Create<SharedResource>(), dbContext);
        var sender = new SenderProxy(type =>
        {
            if (type == typeof(IRequestHandler<PlaceOrderCommand, Guid>))
            {
                return placeOrderHandler;
            }

            throw new InvalidOperationException($"Unsupported handler: {type.FullName}");
        });

        var publisherMock = new Mock<IPublisher>();
        var deliveryPricingMock = new Mock<IDeliveryPricingService>();
        deliveryPricingMock
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new PlaceCheckoutOrderCommandHandler(
            dbContext,
            TestPaymentGatewayResolver.Disabled(),
            deliveryPricingMock.Object,
            sender,
            dbContext,
            publisherMock.Object,
            Options.Create(new BankTransferSettingsOptions
            {
                BankName = "Test Bank",
                AccountHolderName = "Zadana Platform",
                Iban = "SA0380000000608010167519",
                AccountNumber = "608010167519",
            }));

        var result = await handler.Handle(
            new PlaceCheckoutOrderCommand(
                customer.Id,
                vendor.Id,
                address.Id,
                null,
                "bank",
                null,
                null),
            CancellationToken.None);

        result.Order.Status.Should().Be("pending");
        result.Order.PaymentStatus.Should().Be("pending");
        result.Payment.Should().NotBeNull();
        result.Payment!.IframeUrl.Should().Be("ShowBankTransferInstructions");
        result.Payment.ProviderReference.Should().StartWith("ZDN");
        result.Payment.ProviderConfig.Should().NotBeNull();

        var savedOrder = await dbContext.Orders.SingleAsync();
        savedOrder.Status.Should().Be(OrderStatus.PendingBankConfirmation);
        savedOrder.PaymentStatus.Should().Be(PaymentStatus.Pending);

        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeliveryCheckMarksAddressAsUndeliverable_ShouldRejectPlacement()
    {
        await using var dbContext = CreateDbContext();

        var customer = new User("Checkout Customer", "checkout.customer.undeliverable@test.com", "01000000130", UserRole.Customer);
        var vendorUser = new User("Checkout Vendor", "checkout.vendor.undeliverable@test.com", "01000000131", UserRole.Vendor);
        var category = new Category("بقالة", "Groceries");
        var product = new MasterProduct("مياه", "Water", "water-test", category.Id);
        product.Publish();

        var vendor = new Zadana.Domain.Modules.Vendors.Entities.Vendor(
            vendorUser.Id,
            "متجر الاختبار",
            "Checkout Test Store",
            "Groceries",
            "1234567891",
            "checkout.vendor.undeliverable@test.com",
            "01000000131");
        vendor.Approve(10m, Guid.NewGuid());
        vendor.UpdateOperationsSettings(true, null, 30);

        var branch = new Zadana.Domain.Modules.Vendors.Entities.VendorBranch(vendor.Id, "Main Branch", "Nasr City", 30.0444m, 31.2357m, "01000000132", 1m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 20m, 5, vendorBranchId: branch.Id);
        var address = new CustomerAddress(customer.Id, "Checkout Customer", "01000000130", "Heliopolis", AddressLabel.Home, city: "Cairo", latitude: 30.1000m, longitude: 31.4000m);
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(20m, 0m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Categories.Add(category);
        dbContext.MasterProducts.Add(product);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.VendorProducts.Add(vendorProduct);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var orderRepository = new OrderRepository(dbContext);
        var placeOrderHandler = new PlaceOrderCommandHandler(orderRepository, TestLocalizer.Create<SharedResource>(), dbContext);
        var sender = new SenderProxy(type =>
        {
            if (type == typeof(IRequestHandler<PlaceOrderCommand, Guid>))
            {
                return placeOrderHandler;
            }

            throw new InvalidOperationException($"Unsupported handler: {type.FullName}");
        });

        var deliveryPricingMock = new Mock<IDeliveryPricingService>();
        deliveryPricingMock
            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(10m, 5m, 0m, 15m, 20m, "zone", "Zone rule", 1m, 5m, 3m, 12m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        var handler = new PlaceCheckoutOrderCommandHandler(
            dbContext,
            TestPaymentGatewayResolver.Disabled(),
            deliveryPricingMock.Object,
            sender,
            dbContext,
            Mock.Of<IPublisher>());

        var act = () => handler.Handle(
            new PlaceCheckoutOrderCommand(
                customer.Id,
                vendor.Id,
                address.Id,
                null,
                "cash",
                null,
                "should fail"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DELIVERY_NOT_AVAILABLE");
    }

    private sealed class SenderProxy : ISender
    {
        private readonly Func<Type, object> _resolver;

        public SenderProxy(Func<Type, object> resolver)
        {
            _resolver = resolver;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
            dynamic handler = _resolver(handlerType);
            return handler.Handle((dynamic)request, cancellationToken);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            var requestInterface = request.GetType()
                .GetInterfaces()
                .First(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>));
            var responseType = requestInterface.GetGenericArguments()[0];
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), responseType);
            dynamic handler = _resolver(handlerType);
            return handler.Handle((dynamic)request, cancellationToken);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return (Task)Send((object)request, cancellationToken);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}

