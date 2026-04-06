using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.AddToCart;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class AddToCartCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private AddToCartCommandHandler CreateHandler() =>
        new(_orderRepositoryMock.Object, TestLocalizer.Create<SharedResource>(), _unitOfWorkMock.Object);

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        _orderRepositoryMock
            .Setup(repository => repository.GetMasterProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MasterProduct?)null);

        var command = new AddToCartCommand(Guid.NewGuid(), Guid.NewGuid(), 2);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCartItemExists_ShouldIncreaseQuantity()
    {
        var userId = Guid.NewGuid();
        var masterProduct = new MasterProduct("Name Ar", "Name En", "name-en", Guid.NewGuid());
        var cart = new Zadana.Domain.Modules.Orders.Entities.Cart(userId);
        cart.Items.Add(new Zadana.Domain.Modules.Orders.Entities.CartItem(cart.Id, masterProduct.Id, masterProduct.NameEn, 1));

        _orderRepositoryMock
            .Setup(repository => repository.GetMasterProductAsync(masterProduct.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(masterProduct);
        _orderRepositoryMock
            .Setup(repository => repository.GetCartAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var command = new AddToCartCommand(userId, masterProduct.Id, 2);
        var handler = CreateHandler();

        var cartId = await handler.Handle(command, CancellationToken.None);

        cartId.Should().Be(cart.Id);
        cart.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(3);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
