using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Commands.StartPaymobCheckout;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/checkout")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class CheckoutController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public CheckoutController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpPost("place-order")]
    public async Task<ActionResult<PlaceCheckoutOrderResponse>> PlaceOrder(
        [FromBody] PlaceCheckoutOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new StartPaymobCheckoutCommand(
                userId,
                request.VendorId,
                request.AddressId,
                request.PaymentMethodId,
                request.Notes,
                request.VendorBranchId,
                request.PromoCode),
            cancellationToken);

        return Ok(new PlaceCheckoutOrderResponse(
            result.Message,
            new PlaceCheckoutOrderSummary(
                result.Order.Id,
                result.Order.Status,
                result.Order.Total,
                result.Order.PaymentMethodId),
            new PlaceCheckoutPaymentSummary(
                result.Payment.Id,
                result.Payment.Provider,
                result.Payment.Status,
                result.Payment.IframeUrl,
                result.Payment.ProviderReference)));
    }
}
