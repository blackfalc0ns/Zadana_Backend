using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using FluentValidation;

namespace Zadana.Application.Modules.Identity.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<TokenPairDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required.");
    }
}
