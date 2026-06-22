using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.VerifyPasswordResetOtp;

public record VerifyPasswordResetOtpCommand(string Identifier, string OtpCode) : IRequest<PasswordResetOtpVerifiedDto>;

public class VerifyPasswordResetOtpCommandValidator : AbstractValidator<VerifyPasswordResetOtpCommand>
{
    public VerifyPasswordResetOtpCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["Identifier"].Value);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .Length(4).WithMessage(localizer["InvalidOtpLength"].Value)
            .WithName(localizer["OtpCode"].Value);
    }
}
