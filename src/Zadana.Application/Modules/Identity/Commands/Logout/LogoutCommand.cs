using MediatR;
using FluentValidation;
using Zadana.Application.Modules.Identity.Interfaces;

namespace Zadana.Application.Modules.Identity.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<Unit>;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IIdentityService _identityService;

    public LogoutCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _identityService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Unit.Value;
    }
}
