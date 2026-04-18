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
using Zadana.Application.Modules.Orders.Queries.GetCartVendors;
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
        var actor = TryGetCartActor();
        if (actor is null)
        {
            return Ok(new CartDto([], new CartSummaryDto(0, 0, null, null, null)));
        }

        var result = await Sender.Send(new GetCartQuery(actor, vendorId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("vendors")]
    public async Task<ActionResult<CartAvailableVendorsDto>> GetCartVendors(CancellationToken cancellationToken = default)
    {
        var actor = TryGetCartActor();
        if (actor is null)
        {
            return Ok(new CartAvailableVendorsDto([]));
        }

        var result = await Sender.Send(new GetCartVendorsQuery(actor), cancellationToken);
        return Ok(result);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartItemMutationResponseDto>> AddItem(
        [FromBody] AddCartItemRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var result = await Sender.Send(
            new AddCartItemCommand(GetRequiredCartActor(), request.ProductId, request.Quantity),
            cancellationToken);

        return Ok(result);
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<ActionResult<CartItemMutationResponseDto>> UpdateItem(
        Guid itemId,
        [FromBody] UpdateCartItemQuantityRequest? request,
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var result = await Sender.Send(
            new UpdateCartItemQuantityCommand(GetRequiredCartActor(), itemId, request.Quantity, vendorId),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartItemRemovalResponseDto>> RemoveItem(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new RemoveCartItemCommand(GetRequiredCartActor(), itemId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<ActionResult<CartClearResponseDto>> ClearCart(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ClearCartCommand(GetRequiredCartActor()), cancellationToken);
        return Ok(result);
    }

    private CartActor? TryGetCartActor()
    {
        var userId = _currentUserService.UserId;
        if (userId.HasValue)
        {
            return CartActor.Create(userId.Value, null);
        }

        var guestId = Request.Headers[GuestDeviceHeader].ToString();
        if (!string.IsNullOrWhiteSpace(guestId))
        {
            return CartActor.Create(null, guestId.Trim());
        }

        return null;
    }

    private CartActor GetRequiredCartActor()
    {
        return TryGetCartActor()
            ?? throw new UnauthorizedException(_localizer["GuestCartHeaderRequired", GuestDeviceHeader]);
    }
}
