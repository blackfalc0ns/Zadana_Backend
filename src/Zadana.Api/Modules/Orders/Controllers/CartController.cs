using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.AddCartItem;
using Zadana.Application.Modules.Orders.Commands.ClearCart;
using Zadana.Application.Modules.Orders.Commands.RemoveCartItem;
using Zadana.Application.Modules.Orders.Commands.UpdateCartItemQuantity;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Queries.GetCart;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/cart")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class CartController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CartController(
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCartQuery(GetCurrentUserId()), cancellationToken);
        return Ok(result);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartItemMutationResponseDto>> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AddCartItemCommand(GetCurrentUserId(), request.ProductId, request.Quantity),
            cancellationToken);

        return Ok(result);
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<ActionResult<CartItemMutationResponseDto>> UpdateItem(
        Guid itemId,
        [FromBody] UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new UpdateCartItemQuantityCommand(GetCurrentUserId(), itemId, request.Quantity),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartItemRemovalResponseDto>> RemoveItem(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new RemoveCartItemCommand(GetCurrentUserId(), itemId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<ActionResult<CartClearResponseDto>> ClearCart(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ClearCartCommand(GetCurrentUserId()), cancellationToken);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        return userId.Value;
    }
}
