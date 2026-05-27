using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.Application.Common.Localization;
using Microsoft.Extensions.Localization;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAccessControlService _accessControlService;
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VerifyOtpCommandHandler(
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IAccessControlService accessControlService,
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _accessControlService = accessControlService;
        _context = context;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Email"].Value]);
        }

        var verificationResult = await _identityAccountService.VerifyRegistrationOtpAsync(
            request.Identifier,
            request.OtpCode,
            cancellationToken);

        if (verificationResult.Status == OtpVerificationStatus.UserNotFound)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", request.Identifier]);
        }

        if (verificationResult.Status == OtpVerificationStatus.Failed)
        {
            var errors = string.Join(", ", verificationResult.Errors ?? []);
            throw new BusinessRuleException("VERIFICATION_FAILED", $"{_localizer["VERIFICATION_FAILED"]}: {errors}");
        }

        if (verificationResult.Status != OtpVerificationStatus.Succeeded || verificationResult.Account == null)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        var user = verificationResult.Account;
        if (user.IsLoginLocked || user.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(user.Id, tokens.RefreshToken, DateTime.UtcNow.AddDays(7)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        DriverOperationalStatusDto? driverStatus = null;
        if (user.Role == UserRole.Driver)
        {
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d => d.UserId == user.Id, cancellationToken);

            if (driver is not null)
            {
                driverStatus = DriverOperationalStatusFactory.Create(
                    driver,
                    isLoginLocked: user.IsLoginLocked,
                    lockedAtUtc: user.LockedAtUtc);
            }
        }

        var access = await _accessControlService.GetEffectiveAccessAsync(user.Id, cancellationToken);
        var userDto = new CurrentUserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.MustChangePassword,
            Access: access,
            ProfilePhotoUrl: user.ProfilePhotoUrl);

        var isVerified = AuthResponseVerificationResolver.Resolve(user, driverStatus);

        return new AuthResponseDto(tokens, userDto, isVerified, _localizer["AccountVerifiedSuccessfully"], driverStatus);
    }
}
