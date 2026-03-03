using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Social.Commands.AddReview;

public record AddReviewCommand(
    Guid OrderId,
    Guid UserId,
    Guid VendorId,
    int Rating,
    string? Comment) : MediatR.IRequest<Guid>;

public class AddReviewCommandValidator : AbstractValidator<AddReviewCommand>
{
    public AddReviewCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.UserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage(x => localizer["InvalidRange"]);

        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage(x => localizer["MaxLength"]);
    }
}
