using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Social.Commands.SendNotification;

public record SendNotificationCommand(
    Guid UserId,
    string Title,
    string Body,
    string? Type) : MediatR.IRequest<Guid>;

public class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(1000).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Type)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
    }
}
