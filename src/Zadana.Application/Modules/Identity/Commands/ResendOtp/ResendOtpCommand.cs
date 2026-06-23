using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Validation;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Commands.ResendOtp;

public record ResendOtpCommand(
    string Identifier,
    OtpResendPurpose Purpose = OtpResendPurpose.Registration,
    bool PurposeExplicitlyProvided = false) : IRequest<AuthResponseDto>;

public class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .Must(identifier => IsEmail(identifier) || IsPhone(identifier))
            .WithMessage(localizer["InvalidIdentifier"].Value)
            .WithName(localizer["Identifier"].Value);

        RuleFor(x => x.Purpose)
            .IsInEnum()
            .WithMessage(localizer["ValidationErrorTitle"].Value);
    }

    private static bool IsEmail(string? value) =>
        EmailValidationRules.IsValidComEmail(value);

    private static bool IsPhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim().All(character => char.IsDigit(character) || character == '+');
}
