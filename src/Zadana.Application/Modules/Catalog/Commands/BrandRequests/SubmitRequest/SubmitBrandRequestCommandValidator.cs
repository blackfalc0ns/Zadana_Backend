using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.SubmitRequest;

public class SubmitBrandRequestCommandValidator : AbstractValidator<SubmitBrandRequestCommand>
{
    public SubmitBrandRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.NameAr).NotEmpty().MinimumLength(2).WithMessage(localizer["MinLength"].Value).MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.NameEn).NotEmpty().MinimumLength(2).WithMessage(localizer["MinLength"].Value).MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.LogoUrl).MaximumLength(1000).WithMessage(localizer["ImageUrlTooLong"].Value);
    }
}
