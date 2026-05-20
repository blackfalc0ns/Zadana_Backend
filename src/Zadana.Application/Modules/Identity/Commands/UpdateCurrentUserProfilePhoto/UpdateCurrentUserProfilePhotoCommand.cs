using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.UpdateCurrentUserProfilePhoto;

public record UpdateCurrentUserProfilePhotoCommand(string? ProfilePhotoUrl) : IRequest<CurrentUserDto>;

public class UpdateCurrentUserProfilePhotoCommandValidator : AbstractValidator<UpdateCurrentUserProfilePhotoCommand>
{
    public UpdateCurrentUserProfilePhotoCommandValidator()
    {
        RuleFor(x => x.ProfilePhotoUrl)
            .MaximumLength(1000)
            .Must(BeAValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.ProfilePhotoUrl))
            .WithMessage("Profile photo URL must be a valid absolute http or https URL.");
    }

    private static bool BeAValidUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
