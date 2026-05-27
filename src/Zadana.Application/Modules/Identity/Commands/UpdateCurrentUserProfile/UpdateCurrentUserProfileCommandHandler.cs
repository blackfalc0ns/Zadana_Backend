using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.UpdateCurrentUserProfile;

public class UpdateCurrentUserProfileCommandHandler : IRequestHandler<UpdateCurrentUserProfileCommand, CurrentUserDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IIdentityService _identityService;
    private readonly IEmailVerificationSender _emailVerificationSender;
    private readonly IAccessControlService _accessControlService;
    private readonly IApplicationDbContext _context;

    public UpdateCurrentUserProfileCommandHandler(
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IIdentityService identityService,
        IEmailVerificationSender emailVerificationSender,
        IAccessControlService accessControlService,
        IApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _identityService = identityService;
        _emailVerificationSender = emailVerificationSender;
        _accessControlService = accessControlService;
        _context = context;
    }

    public async Task<CurrentUserDto> Handle(UpdateCurrentUserProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var updateResult = await _identityAccountService.UpdateProfileAsync(
            userId,
            request.FullName,
            request.Email,
            request.Phone,
            cancellationToken);

        if (!updateResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_UPDATE_FAILED", string.Join(", ", updateResult.Errors ?? []));
        }

        if (updateResult.EmailChanged && updateResult.Account is not null)
        {
            await _emailVerificationSender.SendAsync(userId, cancellationToken);
            var access = await _accessControlService.GetEffectiveAccessAsync(userId, cancellationToken);
            var favoritesCount = await _context.CustomerFavorites.CountAsync(x => x.UserId == userId, cancellationToken);

            return new CurrentUserDto(
                updateResult.Account.Id,
                updateResult.Account.FullName,
                updateResult.Account.Email,
                updateResult.Account.PhoneNumber,
                updateResult.Account.Role.ToString(),
                updateResult.Account.MustChangePassword,
                favoritesCount,
                access,
                updateResult.Account.ProfilePhotoUrl);
        }

        return await _identityService.GetCurrentUserAsync(cancellationToken);
    }
}
