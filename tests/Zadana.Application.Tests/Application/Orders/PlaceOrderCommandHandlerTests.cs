using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class PlaceOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private PlaceOrderCommandHandler CreateHandler() =>
        new(_orderRepositoryMock.Object, TestLocalizer.Create<SharedResource>(), _unitOfWorkMock.Object);

    [Fact]
    public async Task Handle_WhenCartEmpty_ShouldThrowBusinessRuleException()
    {
        _orderRepositoryMock
            .Setup(repository => repository.GetCartForCheckoutAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        var command = new PlaceOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CashOnDelivery", null, null, null);
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
            .Setup(repository => repository.GetVendorProductsForCheckoutAsync(vendorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VendorProduct>());

        var command = new PlaceOrderCommand(userId, vendorId, Guid.NewGuid(), "CashOnDelivery", null, null, null);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "VENDOR_MISSING_CART_PRODUCT");
    }
}
