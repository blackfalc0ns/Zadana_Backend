using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Payments.DTOs;

namespace Zadana.Application.Modules.Payments.Commands.StartPaymobCheckout;

public record StartPaymobCheckoutCommand(
    Guid UserId,
    Guid VendorId,
    Guid CustomerAddressId,
    string PaymentMethodId,
    string? Notes,
    Guid? VendorBranchId,
    string? PromoCode) : IRequest<PaymobCheckoutResponseDto>;

public class StartPaymobCheckoutCommandValidator : AbstractValidator<StartPaymobCheckoutCommand>
{
    public StartPaymobCheckoutCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.CustomerAddressId).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.PromoCode).MaximumLength(100);
    }
}
