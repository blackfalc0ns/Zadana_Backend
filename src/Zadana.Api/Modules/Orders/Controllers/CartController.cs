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
using Zadana.Application.Modules.Checkout.Queries.GetCartDeliveryCheck;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Queries.GetCart;
using Zadana.Application.Modules.Orders.Queries.GetCartVendors;
using Zadana.Application.Modules.Orders.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/cart")]
[Tags("Customer App API")]
public class CartController : ApiControllerBase
{
    private const string GuestDeviceHeader = "X-Device-Id";

    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly Zadana.Api.Security.GuestCartSigner _guestCartSigner;

    public CartController(
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer,
        Zadana.Api.Security.GuestCartSigner guestCartSigner)
    {
        _currentUserService = currentUserService;
        _localizer = localizer;
        _guestCartSigner = guestCartSigner;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<CartDto>> GetCart(
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var actor = TryGetCartActor();
        if (actor is null)
        {
            return Ok(new CartDto([], new CartSummaryDto(0, 0, null, null, null), 0, page, perPage));
        }

        var resolvedVendorId = ResolveVendorIdQueryAlias(vendorId);
        var result = await Sender.Send(new GetCartQuery(actor, resolvedVendorId, page, perPage), cancellationToken);
        return Ok(result);
    }

    [HttpGet("vendors")]
    [AllowAnonymous]
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

    [HttpGet("delivery-check")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<GetCartDeliveryCheckResponse>> GetDeliveryCheck(
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        [FromQuery(Name = "address_id")] Guid? addressId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var resolvedVendorId = ResolveVendorIdQueryAlias(vendorId);
        var resolvedAddressId = ResolveAddressIdQueryAlias(addressId);
        var result = await Sender.Send(new GetCartDeliveryCheckQuery(userId, resolvedVendorId, resolvedAddressId), cancellationToken);

        return Ok(new GetCartDeliveryCheckResponse(
            result.AddressId,
            result.SelectedAddress == null
                ? null
                : new CheckoutSelectedAddressResponse(
                    result.SelectedAddress.Id,
                    result.SelectedAddress.Label,
                    result.SelectedAddress.AddressLine,
                    result.SelectedAddress.IsDefault),
            CheckoutController.MapDeliveryCheck(result.DeliveryCheck),
            new CheckoutDeliveryQuoteResponse(
                result.DeliveryQuote.DistanceKm,
                result.DeliveryQuote.BaseFee,
                result.DeliveryQuote.DistanceFee,
                result.DeliveryQuote.SurgeFee,
                result.DeliveryQuote.TotalFee,
                result.DeliveryQuote.PricingMode,
                result.DeliveryQuote.RuleLabel)));
    }

    [HttpPost("items")]
    [AllowAnonymous]
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
    [AllowAnonymous]
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

        var resolvedVendorId = ResolveVendorIdQueryAlias(vendorId);
        var result = await Sender.Send(
            new UpdateCartItemQuantityCommand(GetRequiredCartActor(), itemId, request.Quantity, resolvedVendorId),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CartItemRemovalResponseDto>> RemoveItem(
        Guid itemId,
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedVendorId = ResolveVendorIdQueryAlias(vendorId);
        var result = await Sender.Send(new RemoveCartItemCommand(GetRequiredCartActor(), itemId, resolvedVendorId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [AllowAnonymous]
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
        if (string.IsNullOrWhiteSpace(guestId))
        {
            return null;
        }

        // Cart mutations (POST/PATCH/DELETE) require a signed device id so a
        // bad actor can't guess another guest's id and tamper with their cart.
        // Read-only GETs accept an unsigned id for backward compatibility but
        // the mobile app should always sign once the device-token endpoint is
        // wired in.
        var signature = Request.Headers[Zadana.Api.Security.GuestCartSigner.SignatureHeader].ToString();
        var isMutation = HttpMethods.IsPost(Request.Method) ||
                         HttpMethods.IsPatch(Request.Method) ||
                         HttpMethods.IsDelete(Request.Method) ||
                         HttpMethods.IsPut(Request.Method);

        if (isMutation)
        {
            if (string.IsNullOrWhiteSpace(signature) || !_guestCartSigner.Verify(guestId, signature))
            {
                throw new UnauthorizedException(
                    "Mutating the guest cart requires a signed device id. POST /api/cart/guest-token first.",
                    "GUEST_CART_SIGNATURE_REQUIRED");
            }
        }

        return CartActor.Create(null, guestId.Trim());
    }

    private CartActor GetRequiredCartActor()
    {
        return TryGetCartActor()
            ?? throw new UnauthorizedException(_localizer["GuestCartHeaderRequired", GuestDeviceHeader]);
    }

    private Guid? ResolveVendorIdQueryAlias(Guid? currentValue)
    {
        if (currentValue.HasValue)
        {
            return currentValue;
        }

        if (!Request.Query.TryGetValue("vendorId", out var values))
        {
            return null;
        }

        var value = values.Count > 0 ? values[0] : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new BadRequestException("INVALID_VENDOR_ID", "vendorId must be a valid GUID.");
    }

    private Guid? ResolveAddressIdQueryAlias(Guid? currentValue)
    {
        if (currentValue.HasValue)
        {
            return currentValue;
        }

        if (!Request.Query.TryGetValue("addressId", out var values))
        {
            return null;
        }

        var value = values.Count > 0 ? values[0] : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new BadRequestException("INVALID_ADDRESS_ID", "addressId must be a valid GUID.");
    }
}
