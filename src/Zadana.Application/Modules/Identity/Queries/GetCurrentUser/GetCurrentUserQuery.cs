using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;

namespace Zadana.Application.Modules.Identity.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<CurrentUserDto>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    private readonly IIdentityService _identityService;

    public GetCurrentUserQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        return await _identityService.GetCurrentUserAsync(cancellationToken);
    }
}
