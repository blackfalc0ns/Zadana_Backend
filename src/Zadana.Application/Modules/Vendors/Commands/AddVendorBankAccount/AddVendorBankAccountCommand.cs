using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorBankAccount;

public record AddVendorBankAccountCommand(
    Guid VendorId,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? AccountNumber,
    string? SwiftCode,
    bool IsPrimary) : MediatR.IRequest<Guid>;

public class AddVendorBankAccountCommandValidator : AbstractValidator<AddVendorBankAccountCommand>
{
    public AddVendorBankAccountCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.AccountHolderName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Iban)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.AccountNumber)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.SwiftCode)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
    }
}
