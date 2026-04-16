using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/orders")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class OrdersController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public OrdersController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<ActionResult<PlaceOrderResponse>> PlaceOrder(
        [FromBody] PlaceOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new PlaceCheckoutOrderCommand(
                userId,
                request.AddressId,
                request.DeliverySlotId,
                request.PaymentMethod,
                request.PromoCode,
                request.Notes),
            cancellationToken);

        return Ok(CheckoutController.MapPlacedOrder(result));
    }
}
