using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.VendorGoogleAuth;

public sealed class VendorGoogleAuthCommandHandler : IRequestHandler<VendorGoogleAuthCommand, VendorGoogleAuthResultDto>
{
    private static readonly UserRole[] AllowedRoles = [UserRole.Vendor, UserRole.VendorStaff];

    private readonly IGoogleIdTokenVerifier _googleIdTokenVerifier;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IAccessControlService _accessControlService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VendorGoogleAuthCommandHandler(
        IGoogleIdTokenVerifier googleIdTokenVerifier,
        IIdentityAccountService identityAccountService,
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore refreshTokenStore,
        IAccessControlService accessControlService,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResource> localizer)
    {
        _googleIdTokenVerifier = googleIdTokenVerifier;
        _identityAccountService = identityAccountService;
        _jwtTokenService = jwtTokenService;
        _refreshTokenStore = refreshTokenStore;
        _accessControlService = accessControlService;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<VendorGoogleAuthResultDto> Handle(VendorGoogleAuthCommand request, CancellationToken cancellationToken)
    {
        var profile = await _googleIdTokenVerifier.VerifyAsync(request.IdToken, cancellationToken);
        var existing = await _identityAccountService.FindByIdentifierAsync(profile.Email, cancellationToken);

        if (existing is null)
        {
            return new VendorGoogleAuthResultDto(
                "continue_registration",
                Profile: new VendorGoogleProfileDto(
                    profile.Email,
                    profile.FullName,
                    profile.GivenName,
                    profile.FamilyName,
                    profile.Subject));
        }

        if (!AllowedRoles.Contains(existing.Role))
        {
            throw new BusinessRuleException(
                "GOOGLE_ACCOUNT_WRONG_APP",
                "This Google account is already registered for another Zadana app. Use a different Google account or sign in from the correct app.");
        }

        if (existing.ArchivedAtUtc.HasValue)
        {
            throw new UnauthorizedException(_localizer["ACCOUNT_CLOSED"], "ACCOUNT_CLOSED");
        }

        if (existing.IsLoginLocked || existing.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(
                _localizer["AccountLoginDenied", existing.AccountStatus],
                "ACCOUNT_LOGIN_DENIED");
        }

        if (!existing.EmailConfirmed)
        {
            var confirmResult = await _identityAccountService.ConfirmEmailAsync(existing.Id, cancellationToken);
            if (!confirmResult.Succeeded || confirmResult.Account is null)
            {
                throw new BusinessRuleException(
                    "IDENTITY_OPERATION_FAILED",
                    _localizer["IDENTITY_OPERATION_FAILED"]);
            }

            existing = confirmResult.Account;
        }

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(existing, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(
            existing.Id,
            tokens.RefreshToken,
            DateTime.UtcNow.AddDays(7)));

        var recordLoginResult = await _identityAccountService.RecordLoginAsync(existing.Id, cancellationToken);
        if (!recordLoginResult.Succeeded)
        {
            throw new BusinessRuleException(
                "IDENTITY_OPERATION_FAILED",
                _localizer["IDENTITY_OPERATION_FAILED"]);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var access = await _accessControlService.GetEffectiveAccessAsync(existing.Id, cancellationToken);
        var userDto = new CurrentUserDto(
            existing.Id,
            existing.FullName,
            existing.Email,
            existing.PhoneNumber,
            existing.Role.ToString(),
            existing.MustChangePassword,
            Access: access,
            ProfilePhotoUrl: existing.ProfilePhotoUrl);

        var auth = new AuthResponseDto(tokens, userDto, IsVerified: true);
        return new VendorGoogleAuthResultDto("login", Auth: auth);
    }
}
