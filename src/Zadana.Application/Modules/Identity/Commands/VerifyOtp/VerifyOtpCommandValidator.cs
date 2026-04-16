using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .Must(identifier => IsEmail(identifier) || IsPhone(identifier))
            .WithMessage(localizer["InvalidEmail"].Value)
            .WithName(localizer["Identifier"].Value);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .Length(4).WithMessage(localizer["InvalidOtpLength"].Value)
            .WithName(localizer["OtpCode"].Value);
    }

    private static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@');

    private static bool IsPhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim().All(character => char.IsDigit(character) || character == '+');
}
