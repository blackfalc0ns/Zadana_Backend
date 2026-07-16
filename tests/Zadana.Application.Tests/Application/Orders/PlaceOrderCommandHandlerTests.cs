using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Modules.Orders.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class PlaceOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private PlaceOrderCommandHandler CreateHandler() =>
        new(_orderRepositoryMock.Object, TestLocalizer.Create<SharedResource>(), _unitOfWorkMock.Object);

    [Fact]
    public async Task GetVendorProductsForCheckoutAsync_WhenVendorProductIsUnavailable_ShouldExcludeIt()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new ApplicationDbContext(options, new AuditableEntityInterceptor());

        var vendorUser = new User("Unavailable Vendor", "placeorder.unavailable.vendor@test.com", "01000000531", UserRole.Vendor);
        var category = new Category("Groceries", "Groceries");
        var product = new MasterProduct("Unavailable Juice", "Unavailable Juice", "unavailable-juice-test", category.Id);
        product.Publish();

        var vendor = new Zadana.Domain.Modules.Vendors.Entities.Vendor(
            vendorUser.Id,
            "Unavailable PlaceOrder Store",
            "Unavailable PlaceOrder Store",
            "Groceries",
            "1234567895",
            "placeorder.unavailable.vendor@test.com",
            "01000000531");
        vendor.Approve(10m, Guid.NewGuid());

        var branch = new Zadana.Domain.Modules.Vendors.Entities.VendorBranch(vendor.Id, "Main Branch", "Nasr City", 30.0444m, 31.2357m, "01000000532", 15m);
        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 30m, 10, tradePrice: 20m, vendorBranchId: branch.Id);
        vendorProduct.SetAvailability(false);

        dbContext.Users.Add(vendorUser);
        dbContext.Categories.Add(category);
        dbContext.MasterProducts.Add(product);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.VendorProducts.Add(vendorProduct);
        await dbContext.SaveChangesAsync();

        var repository = new OrderRepository(dbContext);

        var result = await repository.GetVendorProductsForCheckoutAsync(
            vendor.Id,
            [product.Id],
            branch.Id,
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCartEmpty_ShouldThrowBusinessRuleException()
    {
        _orderRepositoryMock
            .Setup(repository => repository.GetCartForCheckoutAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _orderRepositoryMock
            .Setup(repository => repository.GetReusablePendingOrderForCheckoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<PaymentMethodType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyDictionary<Guid, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new PlaceOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CashOnDelivery", null, null, null, 0m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "EMPTY_CART");
    }

    [Fact]
    public async Task Handle_WhenVendorDoesNotOfferAllCartProducts_ShouldThrowBusinessRuleException()
    {
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var masterProduct = new MasterProduct("Name Ar", "Name En", "name-en", Guid.NewGuid());
        var cart = new Cart(userId);
        cart.Items.Add(new CartItem(cart.Id, masterProduct.Id, masterProduct.NameEn, 2));

        _orderRepositoryMock
            .Setup(repository => repository.GetCartForCheckoutAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _orderRepositoryMock
            .Setup(repository => repository.GetVendorProductsForCheckoutAsync(vendorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VendorProduct>());
        _orderRepositoryMock
            .Setup(repository => repository.GetReusablePendingOrderForCheckoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<PaymentMethodType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyDictionary<Guid, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new PlaceOrderCommand(userId, vendorId, Guid.NewGuid(), "CashOnDelivery", null, null, null, 0m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "CART_ITEMS_UNAVAILABLE_AT_ADDRESS_BRANCH");
    }

    [Theory]
    [InlineData("Wallet")]
    [InlineData("Mada")]
    [InlineData("ApplePay")]
    public async Task Handle_WhenPaymentMethodRequiresDedicatedFundingFlow_ShouldThrowBusinessRuleException(string paymentMethod)
    {
        var userId = Guid.NewGuid();
        var masterProduct = new MasterProduct("Name Ar", "Name En", "name-en", Guid.NewGuid());
        var cart = new Cart(userId);
        cart.Items.Add(new CartItem(cart.Id, masterProduct.Id, masterProduct.NameEn, 1));

        _orderRepositoryMock
            .Setup(repository => repository.GetCartForCheckoutAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var command = new PlaceOrderCommand(userId, Guid.NewGuid(), Guid.NewGuid(), paymentMethod, null, null, null, 0m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "PAYMENT_METHOD_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Handle_WhenMatchingPendingCardOrderExists_ShouldReuseIt()
    {
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var existingOrder = new Order(
            "ORD-EXISTING",
            userId,
            vendorId,
            addressId,
            PaymentMethodType.Card,
            120m,
            0m,
            20m,
            20m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
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
            6m);
        var masterProduct = new MasterProduct("Name Ar", "Name En", "name-en", Guid.NewGuid());
        var cart = new Cart(userId);
        cart.Items.Add(new CartItem(cart.Id, masterProduct.Id, masterProduct.NameEn, 2));
        cart.UpdateTotals(120m, 20m);
        var vendorProduct = new VendorProduct(vendorId, masterProduct.Id, 60m, 10, tradePrice: 60m);

        _orderRepositoryMock
            .Setup(repository => repository.GetCartForCheckoutAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _orderRepositoryMock
            .Setup(repository => repository.GetVendorProductsForCheckoutAsync(vendorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VendorProduct> { [masterProduct.Id] = vendorProduct });
        _orderRepositoryMock
            .Setup(repository => repository.GetReusablePendingOrderForCheckoutAsync(
                userId,
                vendorId,
                addressId,
                PaymentMethodType.Card,
                null,
                null,
                null,
                120m,
                0m,
                20m,
                20m,
                0m,
                0m,
                null,
                null,
                null,
                0m,
                0m,
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
                0m,
                0m,
                0m,
                It.IsAny<IReadOnlyDictionary<Guid, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new PlaceOrderCommand(userId, vendorId, addressId, "Card", null, null, null, 20m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m, ClearCartAfterPlacement: false),
            CancellationToken.None);

        result.Should().Be(existingOrder.Id);
        _orderRepositoryMock.Verify(repository => repository.AddOrder(It.IsAny<Order>()), Times.Never);
        _orderRepositoryMock.Verify(repository => repository.AddOrderItem(It.IsAny<OrderItem>()), Times.Never);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTradePriceMissing_ShouldThrowBusinessRuleException()
    {
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var masterProduct = new MasterProduct("Name Ar", "Name En", "name-en", Guid.NewGuid());
        var cart = new Cart(userId);
        cart.Items.Add(new CartItem(cart.Id, masterProduct.Id, masterProduct.NameEn, 1));

        _orderRepositoryMock
            .Setup(repository => repository.GetCartForCheckoutAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _orderRepositoryMock
            .Setup(repository => repository.GetVendorProductsForCheckoutAsync(vendorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VendorProduct>
            {
                [masterProduct.Id] = new VendorProduct(vendorId, masterProduct.Id, 40m, 5)
            });

        var handler = CreateHandler();

        var act = () => handler.Handle(
            new PlaceOrderCommand(userId, vendorId, Guid.NewGuid(), "CashOnDelivery", null, null, null, 0m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "INCOMPLETE_VENDOR_PRICING");
    }
}
