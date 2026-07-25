using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.Commands.ApplyCheckoutPromoCode;
using Zadana.Application.Modules.Checkout.Commands.RemoveCheckoutPromoCode;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
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
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        [FromQuery(Name = "address_id")] Guid? addressId = null,
        [FromQuery(Name = "delivery_slot_id")] string? deliverySlotId = null,
        [FromQuery(Name = "payment_method")] string? paymentMethod = null,
        [FromQuery(Name = "fulfillment_type")] string? fulfillmentType = null,
        [FromQuery(Name = "vendor_branch_id")] Guid? vendorBranchId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var resolvedVendorId = ResolveGuidQueryAlias(vendorId, "vendorId", "INVALID_VENDOR_ID");
        var resolvedAddressId = ResolveGuidQueryAlias(addressId, "addressId", "INVALID_ADDRESS_ID");
        var resolvedDeliverySlotId = ResolveStringQueryAlias(deliverySlotId, "deliverySlotId");
        var resolvedPaymentMethod = ResolveStringQueryAlias(paymentMethod, "paymentMethod");
        var resolvedFulfillmentType = ResolveStringQueryAlias(fulfillmentType, "fulfillmentType");
        var resolvedVendorBranchId = ResolveGuidQueryAlias(vendorBranchId, "vendorBranchId", "INVALID_VENDOR_BRANCH_ID");
        var fulfillment = string.Equals(resolvedFulfillmentType, "pickup", StringComparison.OrdinalIgnoreCase)
            ? FulfillmentType.Pickup
            : FulfillmentType.Delivery;

        var result = await Sender.Send(
            new GetCheckoutSummaryQuery(
                userId,
                resolvedVendorId,
                resolvedAddressId,
                resolvedDeliverySlotId,
                resolvedPaymentMethod,
                fulfillment,
                resolvedVendorBranchId),
            cancellationToken);

        return Ok(MapSummary(result));
    }

    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<ActionResult<CheckoutConfigResponse>> GetConfig(
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = await context.PlatformPickupSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == PlatformPickupSettings.SingletonId, cancellationToken);

        var cashOnPickupEnabled = settings?.PickupCashOnPickupEnabled ?? false;
        var allowedPickupPayments = cashOnPickupEnabled
            ? new[] { "card", "apple_pay", "cash" }
            : new[] { "card", "apple_pay" };

        return Ok(new CheckoutConfigResponse(
            settings?.DeliveryOptionEnabled ?? true,
            settings?.PickupOptionEnabled ?? true,
            cashOnPickupEnabled,
            allowedPickupPayments));
    }

    [HttpPost("promo-code")]
    public async Task<ActionResult<ApplyCheckoutPromoCodeResponse>> ApplyPromoCode(
        [FromBody] ApplyCheckoutPromoCodeRequest? request,
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        [FromQuery(Name = "payment_method")] string? paymentMethod = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var resolvedVendorId = ResolveGuidQueryAlias(vendorId, "vendorId", "INVALID_VENDOR_ID");
        var resolvedPaymentMethod = ResolveStringQueryAlias(paymentMethod, "paymentMethod");
        var result = await Sender.Send(new ApplyCheckoutPromoCodeCommand(userId, resolvedVendorId, request.Code, resolvedPaymentMethod), cancellationToken);
        var checkout = await Sender.Send(
            new GetCheckoutSummaryQuery(userId, resolvedVendorId, null, null, resolvedPaymentMethod),
            cancellationToken);

        return Ok(MapApplyPromoCodeResponse(result.Message, checkout));
    }

    [HttpDelete("promo-code")]
    public async Task<ActionResult<RemoveCheckoutPromoCodeResponse>> RemovePromoCode(
        [FromQuery(Name = "vendor_id")] Guid? vendorId = null,
        [FromQuery(Name = "payment_method")] string? paymentMethod = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var resolvedVendorId = ResolveGuidQueryAlias(vendorId, "vendorId", "INVALID_VENDOR_ID");
        var resolvedPaymentMethod = ResolveStringQueryAlias(paymentMethod, "paymentMethod");
        var result = await Sender.Send(new RemoveCheckoutPromoCodeCommand(userId, resolvedVendorId, resolvedPaymentMethod), cancellationToken);
        var checkout = await Sender.Send(
            new GetCheckoutSummaryQuery(userId, resolvedVendorId, null, null, resolvedPaymentMethod),
            cancellationToken);

        return Ok(MapRemovePromoCodeResponse(result.Message, checkout));
    }

    private static GetCheckoutSummaryResponse MapSummary(CheckoutSummaryDto result)
    {
        var isPickup = string.Equals(result.FulfillmentType, "pickup", StringComparison.OrdinalIgnoreCase);
        var canExposeDeliveryEstimate = !isPickup &&
            result.DeliveryCheck.IsDeliverable &&
            result.DeliveryCheck.CanProceedToCheckout;
        var canExposePickupEstimate = isPickup && result.DeliveryCheck.CanProceedToCheckout;
        CheckoutEstimatedDeliveryWindowResponse? estimatedDeliveryWindow = null;
        if (canExposeDeliveryEstimate || canExposePickupEstimate)
        {
            estimatedDeliveryWindow = new CheckoutEstimatedDeliveryWindowResponse(
                result.EstimatedDeliveryWindow.MinMinutes,
                result.EstimatedDeliveryWindow.MaxMinutes,
                result.EstimatedDeliveryWindow.Title,
                result.EstimatedDeliveryWindow.Label,
                result.EstimatedDeliveryWindow.Subtitle,
                result.EstimatedDeliveryWindow.Confidence,
                result.EstimatedDeliveryWindow.Source,
                result.EstimatedDeliveryWindow.IsApproximate,
                result.EstimatedDeliveryWindow.CalculationMode,
                result.EstimatedDeliveryWindow.Explanation);
        }

        CheckoutDeliveryQuoteResponse? deliveryQuote = canExposeDeliveryEstimate || isPickup
            ? new CheckoutDeliveryQuoteResponse(
                result.DeliveryQuote.DistanceKm,
                result.DeliveryQuote.BaseFee,
                result.DeliveryQuote.DistanceFee,
                result.DeliveryQuote.SurgeFee,
                result.DeliveryQuote.TotalFee,
                result.DeliveryQuote.PricingMode,
                result.DeliveryQuote.RuleLabel)
            : null;
        CheckoutDeliveryBreakdownResponse? deliveryBreakdown = canExposeDeliveryEstimate || isPickup
            ? new CheckoutDeliveryBreakdownResponse(
                new CheckoutDeliveryLegResponse(
                    result.DeliveryBreakdown.DriverToVendor.DistanceKm,
                    result.DeliveryBreakdown.DriverToVendor.Fee,
                    result.DeliveryBreakdown.DriverToVendor.PricingSource),
                new CheckoutDeliveryLegResponse(
                    result.DeliveryBreakdown.VendorToCustomer.DistanceKm,
                    result.DeliveryBreakdown.VendorToCustomer.Fee,
                    result.DeliveryBreakdown.VendorToCustomer.PricingSource),
                result.DeliveryBreakdown.TotalDelivery,
                result.DeliveryBreakdown.PricingMode,
                result.DeliveryBreakdown.UsedEstimatedDriverPricing)
            : null;

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
                    item.TotalPrice,
                    item.VariantDisplaySize,
                    item.PackageTypeName,
                    item.MeasurementValue,
                    item.MeasurementUnitName,
                    item.VariantImageUrl,
                    item.VariantImages)).ToList(),
                result.Cart.HasUnavailableItems,
                result.Cart.UnavailableItemsCount,
                result.Cart.RequiresUnavailableItemsConfirmation,
                (result.Cart.UnavailableItems ?? new List<CheckoutUnavailableCartItemDto>())
                    .Select(item => new CheckoutUnavailableCartItemResponse(
                        item.Id,
                        item.ProductId,
                        item.Name,
                        item.Quantity,
                        item.AvailabilityStatus))
                    .ToList()),
            result.SelectedAddress?.Id,
            result.SelectedAddress == null
                ? null
                : new CheckoutSelectedAddressResponse(
                    result.SelectedAddress.Id,
                    result.SelectedAddress.Label,
                    result.SelectedAddress.AddressLine,
                    result.SelectedAddress.IsDefault),
            result.AvailableAddresses.Select(addr => new CheckoutSelectedAddressResponse(
                addr.Id,
                addr.Label,
                addr.AddressLine,
                addr.IsDefault)).ToList(),
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
                method.Description,
                method.IsAvailable,
                method.IsDefault)).ToList(),
            result.PromoCode == null
                ? null
                : new CheckoutPromoCodeResponse(
                    result.PromoCode.Code,
                    result.PromoCode.DiscountType,
                    result.PromoCode.DiscountValue,
                    result.PromoCode.DiscountAmount),
            MapDeliveryCheck(result.DeliveryCheck),
            estimatedDeliveryWindow,
            deliveryQuote,
            deliveryBreakdown,
            result.ShippingBreakdown.Select(item => new CheckoutShippingBreakdownLineResponse(
                item.Code,
                item.Label,
                item.Amount)).ToList(),
            result.PricingMode,
            new CheckoutSummaryTotalsResponse(
                result.Summary.Subtotal,
                result.Summary.ShippingCost,
                result.Summary.Discount,
                result.Summary.VatAmount,
                result.Summary.CodFee,
                result.Summary.Total,
                result.Summary.Currency),
            result.FulfillmentType,
            result.PickupBranch == null
                ? null
                : new CheckoutPickupBranchResponse(
                    result.PickupBranch.Id,
                    result.PickupBranch.Name,
                    result.PickupBranch.AddressLine,
                    result.PickupBranch.City));
    }

    private static ApplyCheckoutPromoCodeResponse MapApplyPromoCodeResponse(
        string message,
        CheckoutSummaryDto result)
    {
        var summary = MapSummary(result);

        return new ApplyCheckoutPromoCodeResponse(
            message,
            summary.Cart,
            summary.AddressId,
            summary.SelectedAddress,
            summary.AvailableAddresses,
            summary.DeliverySlots,
            summary.PaymentMethods,
            summary.PromoCode,
            summary.DeliveryCheck,
            summary.EstimatedDeliveryWindow,
            summary.DeliveryQuote,
            summary.DeliveryBreakdown,
            summary.ShippingBreakdown,
            summary.PricingMode,
            summary.Summary);
    }

    private static RemoveCheckoutPromoCodeResponse MapRemovePromoCodeResponse(
        string message,
        CheckoutSummaryDto result)
    {
        var summary = MapSummary(result);

        return new RemoveCheckoutPromoCodeResponse(
            message,
            summary.Cart,
            summary.AddressId,
            summary.SelectedAddress,
            summary.AvailableAddresses,
            summary.DeliverySlots,
            summary.PaymentMethods,
            summary.PromoCode,
            summary.DeliveryCheck,
            summary.EstimatedDeliveryWindow,
            summary.DeliveryQuote,
            summary.DeliveryBreakdown,
            summary.ShippingBreakdown,
            summary.PricingMode,
            summary.Summary);
    }

    internal static CheckoutDeliveryCheckResponse MapDeliveryCheck(CheckoutDeliveryCheckDto result)
    {
        var message = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? result.MessageAr
            : result.MessageEn;

        return new CheckoutDeliveryCheckResponse(
            result.Status,
            result.IsDeliverable,
            result.CanProceedToCheckout,
            message,
            result.MessageAr,
            result.MessageEn,
            result.DeliveryFee,
            result.DistanceKm);
    }

    private Guid? ResolveGuidQueryAlias(Guid? currentValue, string aliasName, string errorCode)
    {
        if (currentValue.HasValue)
        {
            return currentValue;
        }

        if (!Request.Query.TryGetValue(aliasName, out var values))
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

        throw new BadRequestException(errorCode, $"{aliasName} must be a valid GUID.");
    }

    private string? ResolveStringQueryAlias(string? currentValue, string aliasName)
    {
        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            return currentValue;
        }

        if (!Request.Query.TryGetValue(aliasName, out var values))
        {
            return currentValue;
        }

        var value = values.Count > 0 ? values[0] : null;
        return string.IsNullOrWhiteSpace(value) ? currentValue : value;
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
                    result.Payment.ProviderReference,
                    result.Payment.ProviderConfig,
                    result.Payment.PaymentFlow,
                    result.Payment.IsPaid,
                    result.Payment.RequiresCustomerAction,
                    result.Payment.CustomerAction,
                    result.Payment.ConfirmationMode));
    }
}
