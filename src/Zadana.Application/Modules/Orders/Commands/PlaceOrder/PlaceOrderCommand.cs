using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Orders.Commands.PlaceOrder;

public record PlaceOrderCommand(
    Guid UserId,
    Guid VendorId,
    Guid CustomerAddressId,
    string PaymentMethod,
    string? Notes,
    Guid? VendorBranchId,
    Guid? CouponId,
    bool ClearCartAfterPlacement = true) : MediatR.IRequest<Guid>;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
        RuleFor(x => x.VendorId).NotEmpty().WithMessage("Vendor ID is required.");
        RuleFor(x => x.CustomerAddressId).NotEmpty().WithMessage("Customer Address ID is required.");
        
        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Payment method is required.")
            .IsEnumName(typeof(PaymentMethodType), caseSensitive: false)
            .WithMessage("Invalid payment method.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}
