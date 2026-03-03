using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Vendors.Commands.CreateVendor;

public record CreateVendorCommand(
    Guid OwnerUserId,
    string LegalName,
    string DisplayName,
    string? TaxNumber,
    string? CommercialRegister,
    string? SupportPhone,
    string? SupportEmail) : MediatR.IRequest<Guid>;

public class CreateVendorCommandValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.TaxNumber)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.CommercialRegister)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.SupportPhone)
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.SupportEmail)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.SupportEmail)).WithMessage(x => localizer["InvalidEmail"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);
    }
}
