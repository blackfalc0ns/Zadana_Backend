using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.UpdateCurrentUserProfilePhoto;

public class UpdateCurrentUserProfilePhotoCommandHandler : IRequestHandler<UpdateCurrentUserProfilePhotoCommand, CurrentUserDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IIdentityService _identityService;

    public UpdateCurrentUserProfilePhotoCommandHandler(
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IIdentityService identityService)
    {
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _identityService = identityService;
    }

    public async Task<CurrentUserDto> Handle(UpdateCurrentUserProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var updateResult = await _identityAccountService.UpdateProfilePhotoAsync(
            userId,
            request.ProfilePhotoUrl,
            cancellationToken);

        if (!updateResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_UPDATE_FAILED", string.Join(", ", updateResult.Errors ?? []));
        }

        return await _identityService.GetCurrentUserAsync(cancellationToken);
    }
}
