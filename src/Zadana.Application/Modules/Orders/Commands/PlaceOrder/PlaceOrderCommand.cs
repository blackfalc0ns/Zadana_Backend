using FluentValidation;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Orders.Commands.PlaceOrder;

public record PlaceOrderCommand(
    Guid UserId,
    Guid VendorId,
    Guid? CustomerAddressId,
    string PaymentMethod,
    string? Notes,
    Guid? VendorBranchId,
    Guid? CouponId,
    decimal BaseDeliveryFee,
    decimal DistanceDeliveryFee,
    decimal SurgeDeliveryFee,
    decimal? QuotedDistanceKm,
    string? DeliveryPricingMode,
    string? DeliveryPricingRuleLabel,
    decimal DriverToVendorDistanceKm,
    decimal VendorToCustomerDistanceKm,
    decimal DriverToVendorFee,
    decimal VendorToCustomerFee,
    string? DriverToVendorPricingSource = null,
    string? VendorToCustomerPricingSource = null,
    bool UsedEstimatedDriverPricing = false,
    string? PricingOriginType = null,
    Guid? PricingOriginDriverId = null,
    string? DeliveryQuoteStatus = null,
    DateTime? DeliveryQuoteLockedAtUtc = null,
    int DeliveryQuoteVersion = 1,
    bool HasDeliveryAnomalyWarning = false,
    decimal VatAmount = 0m,
    decimal CodFee = 0m,
    bool ClearCartAfterPlacement = true,
    FulfillmentType Fulfillment = FulfillmentType.Delivery,
    decimal? CommissionOverride = null) : MediatR.IRequest<Guid>;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
        RuleFor(x => x.VendorId).NotEmpty().WithMessage("Vendor ID is required.");

        RuleFor(x => x.CustomerAddressId)
            .NotEmpty()
            .When(x => x.Fulfillment == FulfillmentType.Delivery)
            .WithMessage("Customer Address ID is required for delivery orders.");

        RuleFor(x => x.VendorBranchId)
            .NotEmpty()
            .When(x => x.Fulfillment == FulfillmentType.Pickup)
            .WithMessage("Vendor branch is required for pickup orders.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Payment method is required.")
            .IsEnumName(typeof(PaymentMethodType), caseSensitive: false)
            .WithMessage("Invalid payment method.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}
