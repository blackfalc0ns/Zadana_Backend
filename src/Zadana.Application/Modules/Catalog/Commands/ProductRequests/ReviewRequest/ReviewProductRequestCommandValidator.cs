using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public class ReviewProductRequestCommandValidator : AbstractValidator<ReviewProductRequestCommand>
{
    public ReviewProductRequestCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.ProductRequestId)
            .NotEmpty().WithMessage(localizer["RequestIdRequired"].Value);

        RuleFor(v => v.RejectionReason)
            .NotEmpty()
            .When(v => !v.IsApproved)
            .WithMessage(localizer["RejectionReasonRequired"].Value);
            
        RuleFor(v => v.RejectionReason)
            .MaximumLength(500)
            .WithMessage(localizer["RejectionReasonTooLong"].Value);
    }
}
