using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.Commands.ApplyCheckoutPromoCode;
using Zadana.Application.Modules.Checkout.Commands.RemoveCheckoutPromoCode;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;
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

    [HttpGet("summary")]
    public async Task<ActionResult<GetCheckoutSummaryResponse>> GetSummary(
        [FromQuery(Name = "address_id")] Guid? addressId = null,
        [FromQuery(Name = "delivery_slot_id")] string? deliverySlotId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new GetCheckoutSummaryQuery(userId, addressId, deliverySlotId),
            cancellationToken);

        return Ok(MapSummary(result));
    }

    [HttpPost("promo-code")]
    public async Task<ActionResult<ApplyCheckoutPromoCodeResponse>> ApplyPromoCode(
        [FromBody] ApplyCheckoutPromoCodeRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new ApplyCheckoutPromoCodeCommand(userId, request.Code), cancellationToken);

        return Ok(new ApplyCheckoutPromoCodeResponse(
            result.Message,
            new CheckoutPromoCodeResponse(
                result.PromoCode.Code,
                result.PromoCode.DiscountType,
                result.PromoCode.DiscountValue,
                result.PromoCode.DiscountAmount),
            new CheckoutSummaryTotalsResponse(
                result.Summary.Subtotal,
                result.Summary.ShippingCost,
                result.Summary.Discount,
                result.Summary.Total,
                result.Summary.Currency)));
    }

    [HttpDelete("promo-code")]
    public async Task<ActionResult<RemoveCheckoutPromoCodeResponse>> RemovePromoCode(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new RemoveCheckoutPromoCodeCommand(userId), cancellationToken);

        return Ok(new RemoveCheckoutPromoCodeResponse(
            result.Message,
            new CheckoutSummaryTotalsResponse(
                result.Summary.Subtotal,
                result.Summary.ShippingCost,
                result.Summary.Discount,
                result.Summary.Total,
                result.Summary.Currency)));
    }

    private static GetCheckoutSummaryResponse MapSummary(CheckoutSummaryDto result)
    {
        return new GetCheckoutSummaryResponse(
            new CheckoutCartResponse(
                result.Cart.ItemsCount,
                result.Cart.TotalQuantity,
                result.Cart.Items.Select(item => new CheckoutCartItemResponse(
                    item.Id,
                    item.ProductId,
                    item.Name,
                    item.ImageUrl,
                    item.Unit,
                    item.Quantity,
                    item.Price,
                    item.TotalPrice)).ToList()),
            result.SelectedAddress == null
                ? null
                : new CheckoutSelectedAddressResponse(
                    result.SelectedAddress.Id,
                    result.SelectedAddress.Label,
                    result.SelectedAddress.AddressLine,
                    result.SelectedAddress.IsDefault),
            result.DeliverySlots.Select(slot => new CheckoutDeliverySlotResponse(
                slot.Id,
                slot.Label,
                slot.StartAt,
                slot.EndAt,
                slot.IsAvailable,
                slot.IsSelected)).ToList(),
            result.PaymentMethods.Select(method => new CheckoutPaymentMethodResponse(
                method.Code,
                method.Label,
                method.IsAvailable,
                method.IsDefault)).ToList(),
            result.PromoCode == null
                ? null
                : new CheckoutPromoCodeResponse(
                    result.PromoCode.Code,
                    result.PromoCode.DiscountType,
                    result.PromoCode.DiscountValue,
                    result.PromoCode.DiscountAmount),
            new CheckoutSummaryTotalsResponse(
                result.Summary.Subtotal,
                result.Summary.ShippingCost,
                result.Summary.Discount,
                result.Summary.Total,
                result.Summary.Currency));
    }

    internal static PlaceOrderResponse MapPlacedOrder(PlaceCheckoutOrderResultDto result)
    {
        return new PlaceOrderResponse(
            result.Message,
            new PlacedOrderSummaryResponse(
                result.Order.Id,
                result.Order.CreatedAt,
                result.Order.Status,
                result.Order.PaymentMethod,
                result.Order.PaymentStatus,
                result.Order.TotalPrice),
            result.Payment == null
                ? null
                : new CheckoutOrderPaymentResponse(
                    result.Payment.Id,
                    result.Payment.Provider,
                    result.Payment.Status,
                    result.Payment.IframeUrl,
                    result.Payment.ProviderReference));
    }
}
