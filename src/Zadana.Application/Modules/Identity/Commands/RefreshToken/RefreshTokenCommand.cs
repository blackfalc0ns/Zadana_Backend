using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using FluentValidation;

namespace Zadana.Application.Modules.Identity.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken, UserRole[]? ExpectedRoles = null) : IRequest<TokenPairDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required.");
    }
}
