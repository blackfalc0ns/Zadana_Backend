using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.ReviewRequest;

public class ReviewBrandRequestCommandValidator : AbstractValidator<ReviewBrandRequestCommand>
{
    public ReviewBrandRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.BrandRequestId).NotEmpty();
        RuleFor(x => x.RejectionReason).NotEmpty().When(x => !x.IsApproved);
        RuleFor(x => x.RejectionReason).MaximumLength(500);
    }
}
