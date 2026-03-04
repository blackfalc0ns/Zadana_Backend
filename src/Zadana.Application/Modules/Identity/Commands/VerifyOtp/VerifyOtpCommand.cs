using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public record VerifyOtpCommand(string Identifier, string OtpCode) : IRequest<bool>;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .WithName(x => localizer["Identifier"]);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .Length(4).WithMessage(x => localizer["InvalidOtpLength"])
            .WithName(x => localizer["OtpCode"]);
    }
}
