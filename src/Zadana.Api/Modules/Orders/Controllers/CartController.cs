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
using Zadana.Application.Modules.Orders.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/cart")]
[Tags("Customer App API")]
[AllowAnonymous]
public class CartController : ApiControllerBase
{
    private const string GuestDeviceHeader = "X-Device-Id";

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
    public async Task<ActionResult<CartDto>> GetCart(
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCartQuery(GetCartActor(), vendorId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartItemMutationResponseDto>> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AddCartItemCommand(GetCartActor(), request.ProductId, request.Quantity, request.VendorId),
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
            new UpdateCartItemQuantityCommand(GetCartActor(), itemId, request.Quantity),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartItemRemovalResponseDto>> RemoveItem(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new RemoveCartItemCommand(GetCartActor(), itemId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<ActionResult<CartClearResponseDto>> ClearCart(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ClearCartCommand(GetCartActor()), cancellationToken);
        return Ok(result);
    }

    private CartActor GetCartActor()
    {
        var userId = _currentUserService.UserId;
        if (userId.HasValue)
        {
            return CartActor.Create(userId.Value, null);
        }

        var guestId = Request.Headers[GuestDeviceHeader].ToString();
        if (!string.IsNullOrWhiteSpace(guestId))
        {
            return CartActor.Create(null, guestId);
        }

        throw new UnauthorizedException($"{_localizer["UserNotAuthenticated"]}. Send {GuestDeviceHeader} for guest cart access.");
    }
}
