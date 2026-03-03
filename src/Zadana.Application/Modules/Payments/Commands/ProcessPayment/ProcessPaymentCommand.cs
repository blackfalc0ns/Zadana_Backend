using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Payments.Commands.ProcessPayment;

public record ProcessPaymentCommand(
    Guid OrderId,
    string Method,
    decimal Amount) : MediatR.IRequest<Guid>;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        
        RuleFor(x => x.Method)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .IsEnumName(typeof(Domain.Modules.Payments.Enums.PaymentMethodType), caseSensitive: false)
            .WithMessage(x => localizer["InvalidEnum"]);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
    }
}
