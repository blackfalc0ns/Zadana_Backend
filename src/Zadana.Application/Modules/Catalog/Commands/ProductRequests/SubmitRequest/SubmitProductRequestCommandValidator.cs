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
            .NotEmpty().When(v => v.RequestedCategory == null).WithMessage(localizer["RequiredField"].Value)
            .WithName("CategoryId");

        RuleFor(v => v)
            .Must(v => v.SuggestedCategoryId.HasValue || v.RequestedCategory is not null)
            .WithMessage(localizer["RequiredField"].Value);

        RuleFor(v => v.SuggestedDescriptionAr)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .WithName("SuggestedDescriptionAr");

        RuleFor(v => v.SuggestedDescriptionEn)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .WithName("SuggestedDescriptionEn");

        RuleFor(v => v.ImageUrl)
            .MaximumLength(1000).WithMessage(localizer["ImageUrlTooLong"].Value)
            .WithName("ImageUrl");

        When(v => v.RequestedBrand is not null, () =>
        {
            RuleFor(v => v.RequestedBrand!.NameAr)
                .NotEmpty().WithMessage(localizer["RequiredField"].Value)
                .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);

            RuleFor(v => v.RequestedBrand!.NameEn)
                .NotEmpty().WithMessage(localizer["RequiredField"].Value)
                .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);

            RuleFor(v => v.RequestedBrand!.LogoUrl)
                .MaximumLength(1000).When(v => !string.IsNullOrWhiteSpace(v.RequestedBrand!.LogoUrl))
                .WithMessage(localizer["ImageUrlTooLong"].Value);
        });

        When(v => v.RequestedCategory is not null, () =>
        {
            RuleFor(v => v.RequestedCategory!.NameAr)
                .NotEmpty().WithMessage(localizer["RequiredField"].Value)
                .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);

            RuleFor(v => v.RequestedCategory!.NameEn)
                .NotEmpty().WithMessage(localizer["RequiredField"].Value)
                .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);

            RuleFor(v => v.RequestedCategory!.ImageUrl)
                .MaximumLength(1000).When(v => !string.IsNullOrWhiteSpace(v.RequestedCategory!.ImageUrl))
                .WithMessage(localizer["ImageUrlTooLong"].Value);
        });
    }
}
