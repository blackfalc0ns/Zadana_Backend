using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;

namespace Zadana.Application.Modules.Identity.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenPairDto>
{
    private readonly IIdentityService _identityService;

    public RefreshTokenCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<TokenPairDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return await _identityService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
    }
}
