using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;

public class SubmitCategoryRequestCommandValidator : AbstractValidator<SubmitCategoryRequestCommand>
{
    public SubmitCategoryRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.TargetLevel).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DisplayOrder).GreaterThan(0);
        RuleFor(x => x.ImageUrl).MaximumLength(1000).WithMessage(localizer["ImageUrlTooLong"].Value);
    }
}
