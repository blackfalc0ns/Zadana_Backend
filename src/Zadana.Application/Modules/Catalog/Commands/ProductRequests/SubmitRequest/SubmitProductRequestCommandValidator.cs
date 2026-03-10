using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public class SubmitProductRequestCommandValidator : AbstractValidator<SubmitProductRequestCommand>
{
    public SubmitProductRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.SuggestedNameAr)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value)
            .WithName("SuggestedNameAr");

        RuleFor(v => v.SuggestedNameEn)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value)
            .WithName("SuggestedNameEn");

        RuleFor(v => v.SuggestedCategoryId)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName("CategoryId");

        RuleFor(v => v.SuggestedDescriptionAr)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .WithName("SuggestedDescriptionAr");

        RuleFor(v => v.SuggestedDescriptionEn)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .WithName("SuggestedDescriptionEn");

        RuleFor(v => v.ImageUrl)
            .MaximumLength(1000).WithMessage(localizer["ImageUrlTooLong"].Value)
            .WithName("ImageUrl");
    }
}
