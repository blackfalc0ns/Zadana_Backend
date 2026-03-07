using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public class SubmitProductRequestCommandValidator : AbstractValidator<SubmitProductRequestCommand>
{
    public SubmitProductRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.SuggestedNameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "SuggestedNameAr"])
            .MaximumLength(200).WithMessage(localizer["MaxLength", "SuggestedNameAr", 200]);

        RuleFor(v => v.SuggestedNameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "SuggestedNameEn"])
            .MaximumLength(200).WithMessage(localizer["MaxLength", "SuggestedNameEn", 200]);

        RuleFor(v => v.SuggestedCategoryId)
            .NotEmpty().WithMessage(localizer["RequiredField", "CategoryId"]);

        RuleFor(v => v.SuggestedDescriptionAr)
            .MaximumLength(1000).WithMessage(localizer["MaxLength", "SuggestedDescriptionAr", 1000]);

        RuleFor(v => v.SuggestedDescriptionEn)
            .MaximumLength(1000).WithMessage(localizer["MaxLength", "SuggestedDescriptionEn", 1000]);

        RuleFor(v => v.ImageUrl)
            .MaximumLength(1000).WithMessage(localizer["ImageUrlTooLong"]);
    }
}
