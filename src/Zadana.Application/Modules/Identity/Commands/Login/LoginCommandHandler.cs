using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;

namespace Zadana.Application.Modules.Identity.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IIdentityService _identityService;

    public LoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        return await _identityService.LoginAsync(request.Identifier, request.Password, request.ExpectedRoles, cancellationToken);
    }
}
