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
using Zadana.Application.Modules.Orders.Queries.GetCartVendors;
using Zadana.SharedKernel.Exceptions;

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
        _currentUserServiceMock.SetupGet(x => x.IsAuthenticated).Returns(true);
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
        var dto = new CartDto([], new CartSummaryDto(0, 0, null, null, null));
        _senderMock.Setup(x => x.Send(It.IsAny<GetCartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetCart(null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task AddItem_ReturnsOkResult()
    {
        var dto = new CartItemMutationResponseDto(
            "added to cart successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 1, []),
            new CartSummaryDto(1, 1, null, null, null));

        _senderMock.Setup(x => x.Send(It.IsAny<AddCartItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.AddItem(new AddCartItemRequest(Guid.NewGuid(), 1), CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task AddItem_ThrowsBadRequestException_WhenRequestBodyIsMissing()
    {
        var act = () => _controller.AddItem(null!, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Request body is required.");
    }

    [Fact]
    public async Task UpdateItem_ReturnsOkResult()
    {
        var dto = new CartItemMutationResponseDto(
            "cart item updated successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 2, []),
            new CartSummaryDto(1, 2, null, null, null));

        _senderMock.Setup(x => x.Send(It.IsAny<UpdateCartItemQuantityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.UpdateItem(Guid.NewGuid(), new UpdateCartItemQuantityRequest(2), null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task UpdateItem_ThrowsBadRequestException_WhenRequestBodyIsMissing()
    {
        var act = () => _controller.UpdateItem(Guid.NewGuid(), null!, null, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Request body is required.");
    }

    [Fact]
    public async Task UpdateItem_PassesVendorIdToCommand_WhenProvided()
    {
        var dto = new CartItemMutationResponseDto(
            "cart item updated successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 2, []),
            new CartSummaryDto(1, 2, 120m, 20m, 100m));
        UpdateCartItemQuantityCommand? sentCommand = null;

        _senderMock.Setup(x => x.Send(It.IsAny<UpdateCartItemQuantityCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CartItemMutationResponseDto>, CancellationToken>((command, _) => sentCommand = (UpdateCartItemQuantityCommand)command)
            .ReturnsAsync(dto);

        var vendorId = Guid.NewGuid();
        await _controller.UpdateItem(Guid.NewGuid(), new UpdateCartItemQuantityRequest(2), vendorId, CancellationToken.None);

        sentCommand.Should().NotBeNull();
        sentCommand!.VendorId.Should().Be(vendorId);
    }

    [Fact]
    public async Task RemoveItem_ReturnsOkResult()
    {
        var dto = new CartItemRemovalResponseDto("cart item removed successfully", new CartSummaryDto(0, 0, null, null, null));
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

    [Fact]
    public async Task AddItem_AllowsGuest_WhenDeviceHeaderIsProvided()
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _currentUserServiceMock.SetupGet(x => x.IsAuthenticated).Returns(false);
        _controller.ControllerContext.HttpContext.Request.Headers["X-Device-Id"] = "guest-device-123";

        var dto = new CartItemMutationResponseDto(
            "added to cart successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 1, []),
            new CartSummaryDto(1, 1, null, null, null));

        _senderMock.Setup(x => x.Send(It.IsAny<AddCartItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.AddItem(new AddCartItemRequest(Guid.NewGuid(), 1), CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task AddItem_UsesTrimmedGuestHeader_WhenGuestRequestsCartMutation()
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _currentUserServiceMock.SetupGet(x => x.IsAuthenticated).Returns(false);
        _controller.ControllerContext.HttpContext.Request.Headers["X-Device-Id"] = "  guest-device-123  ";

        AddCartItemCommand? sentCommand = null;
        var dto = new CartItemMutationResponseDto(
            "added to cart successfully",
            new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Milk", null, "Liter", 1, []),
            new CartSummaryDto(1, 1, null, null, null));

        _senderMock.Setup(x => x.Send(It.IsAny<AddCartItemCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CartItemMutationResponseDto>, CancellationToken>((command, _) => sentCommand = (AddCartItemCommand)command)
            .ReturnsAsync(dto);

        await _controller.AddItem(new AddCartItemRequest(Guid.NewGuid(), 1), CancellationToken.None);

        sentCommand.Should().NotBeNull();
        sentCommand!.Actor.UserId.Should().BeNull();
        sentCommand.Actor.GuestId.Should().Be("guest-device-123");
    }

    [Fact]
    public async Task AddItem_ThrowsUnauthorizedException_WhenGuestHeaderIsMissing()
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _currentUserServiceMock.SetupGet(x => x.IsAuthenticated).Returns(false);

        var act = () => _controller.AddItem(new AddCartItemRequest(Guid.NewGuid(), 1), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GetCart_PassesVendorIdToQuery()
    {
        var dto = new CartDto([], new CartSummaryDto(0, 0, null, null, null));
        GetCartQuery? sentQuery = null;

        _senderMock.Setup(x => x.Send(It.IsAny<GetCartQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CartDto>, CancellationToken>((query, _) => sentQuery = (GetCartQuery)query)
            .ReturnsAsync(dto);

        var vendorId = Guid.NewGuid();
        await _controller.GetCart(vendorId, CancellationToken.None);

        sentQuery.Should().NotBeNull();
        sentQuery!.VendorId.Should().Be(vendorId);
    }

    [Fact]
    public async Task GetCart_SendsNullVendorId_WhenNotProvided()
    {
        var dto = new CartDto([], new CartSummaryDto(0, 0, null, null, null));
        GetCartQuery? sentQuery = null;

        _senderMock.Setup(x => x.Send(It.IsAny<GetCartQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CartDto>, CancellationToken>((query, _) => sentQuery = (GetCartQuery)query)
            .ReturnsAsync(dto);

        await _controller.GetCart(null, CancellationToken.None);

        sentQuery.Should().NotBeNull();
        sentQuery!.VendorId.Should().BeNull();
    }

    [Fact]
    public async Task GetCartVendors_ReturnsOkResult()
    {
        var dto = new CartAvailableVendorsDto([
            new CartAvailableVendorDto(Guid.NewGuid(), "Green Valley Market", null, 1)
        ]);

        _senderMock.Setup(x => x.Send(It.IsAny<GetCartVendorsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetCartVendors(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }
}
