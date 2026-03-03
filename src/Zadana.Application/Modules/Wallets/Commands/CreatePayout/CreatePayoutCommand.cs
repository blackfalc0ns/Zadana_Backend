using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Wallets.Commands.CreatePayout;

public record CreatePayoutCommand(
    Guid SettlementId,
    Guid VendorBankAccountId,
    decimal Amount) : MediatR.IRequest<Guid>;

public class CreatePayoutCommandValidator : AbstractValidator<CreatePayoutCommand>
{
    public CreatePayoutCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.SettlementId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.VendorBankAccountId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
    }
}
