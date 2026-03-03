using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Wallets.Commands.CreditWallet;

public record CreditWalletCommand(
    WalletOwnerType OwnerType,
    Guid OwnerId,
    decimal Amount,
    string? Description) : MediatR.IRequest<Guid>;

public class CreditWalletCommandValidator : AbstractValidator<CreditWalletCommand>
{
    public CreditWalletCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OwnerType)
            .IsInEnum().WithMessage(x => localizer["InvalidEnum"]);

        RuleFor(x => x.OwnerId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"]);
    }
}
