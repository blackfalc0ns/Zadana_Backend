using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Api.Modules.Orders.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.AddCartItem;
using Zadana.Application.Modules.Orders.Commands.ClearCart;
using Zadana.Application.Modules.Orders.Commands.RemoveCartItem;
using Zadana.Application.Modules.Orders.Commands.UpdateCartItemQuantity;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Queries.GetCart;

namespace Zadana.UnitTests.Modules.Orders;

public class CartControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();
    private readonly CartController _controller;

    public CartControllerTests()
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _localizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _controller = new CartController(_currentUserServiceMock.Object, _localizerMock.Object);

        var services = new ServiceCollection();
        services.AddSingleton(_senderMock.Object);
        var provider = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                RequestServices = provider
            }
        };
    }

    [Fact]
    public async Task GetCart_ReturnsOkResult()
    {
        var dto = new CartDto([], new CartSummaryDto(0, 0));
        _senderMock.Setup(x => x.Send(It.IsAny<GetCartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetCart(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task AddItem_ReturnsOkResult()
    {
        var dto = new CartItemMutationResponseDto(
            "added to cart successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 1, []),
            new CartSummaryDto(1, 1));

        _senderMock.Setup(x => x.Send(It.IsAny<AddCartItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.AddItem(new AddCartItemRequest(Guid.NewGuid(), 1), CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task UpdateItem_ReturnsOkResult()
    {
        var dto = new CartItemMutationResponseDto(
            "cart item updated successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 2, []),
            new CartSummaryDto(1, 2));

        _senderMock.Setup(x => x.Send(It.IsAny<UpdateCartItemQuantityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.UpdateItem(Guid.NewGuid(), new UpdateCartItemQuantityRequest(2), CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task RemoveItem_ReturnsOkResult()
    {
        var dto = new CartItemRemovalResponseDto("cart item removed successfully", new CartSummaryDto(0, 0));
        _senderMock.Setup(x => x.Send(It.IsAny<RemoveCartItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.RemoveItem(Guid.NewGuid(), CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task ClearCart_ReturnsOkResult()
    {
        var dto = new CartClearResponseDto("cart cleared successfully");
        _senderMock.Setup(x => x.Send(It.IsAny<ClearCartCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.ClearCart(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }
}
