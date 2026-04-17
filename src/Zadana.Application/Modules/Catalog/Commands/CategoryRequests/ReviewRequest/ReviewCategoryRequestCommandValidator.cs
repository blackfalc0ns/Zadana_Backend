using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.ReviewRequest;

public class ReviewCategoryRequestCommandValidator : AbstractValidator<ReviewCategoryRequestCommand>
{
    public ReviewCategoryRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CategoryRequestId).NotEmpty();
        RuleFor(x => x.RejectionReason).NotEmpty().When(x => !x.IsApproved);
        RuleFor(x => x.RejectionReason).MaximumLength(500);
        RuleFor(x => x.ApprovedTargetLevel).MaximumLength(50);
    }
}
