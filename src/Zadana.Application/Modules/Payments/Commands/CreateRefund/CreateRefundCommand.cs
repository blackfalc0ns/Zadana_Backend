using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Payments.Commands.CreateRefund;

public record CreateRefundCommand(
    Guid PaymentId,
    decimal Amount,
    string? Reason) : MediatR.IRequest<Guid>;

public class CreateRefundCommandValidator : AbstractValidator<CreateRefundCommand>
{
    public CreateRefundCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.Reason)
            .MaximumLength(300).WithMessage(x => localizer["MaxLength"]);
    }
}
