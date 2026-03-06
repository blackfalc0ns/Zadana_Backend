using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.ChangeStatus;

public class ChangeVendorProductStatusCommandValidator : AbstractValidator<ChangeVendorProductStatusCommand>
{
    public ChangeVendorProductStatusCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage(localizer["RequiredField", "Id"]);

        RuleFor(v => v.VendorId)
            .NotEmpty().WithMessage(localizer["RequiredField", "VendorId"]);
    }
}
