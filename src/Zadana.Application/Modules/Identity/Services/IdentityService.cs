using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Services;

public class IdentityService : IIdentityService
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public IdentityService(
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
        _context = context;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> LoginAsync(string identifier, string password, UserRole[]? expectedRoles = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new UnauthorizedException(_localizer["InvalidCredentials"]);
        }

        var credentialValidation = await _identityAccountService.ValidateCredentialsAsync(identifier, password, cancellationToken);
        if (credentialValidation.Status == CredentialValidationStatus.UserNotFound)
        {
            throw new UnauthorizedException(_localizer["AccountNotFound"]);
        }

        if (credentialValidation.Status == CredentialValidationStatus.InvalidPassword || credentialValidation.Account == null)
        {
            throw new UnauthorizedException(_localizer["InvalidCredentials"]);
        }

        var user = credentialValidation.Account;

        if (expectedRoles != null && expectedRoles.Length > 0 && !expectedRoles.Contains(user.Role))
        {
            throw new UnauthorizedException(_localizer["UnauthorizedAppAccess"]);
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        if (user.IsLoginLocked)
        {
            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        if (RequiresVerifiedCustomerEmail(user))
        {
            throw new BusinessRuleException("ACCOUNT_EMAIL_NOT_VERIFIED", _localizer["AccountEmailNotVerified"]);
        }

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);

        _refreshTokenStore.Add(new NewRefreshToken(
            user.Id,
            tokens.RefreshToken,
            DateTime.UtcNow.AddDays(7)
        ));

        var recordLoginResult = await _identityAccountService.RecordLoginAsync(user.Id, cancellationToken);
        if (!recordLoginResult.Succeeded)
        {
            var errors = string.Join(", ", recordLoginResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var favoritesCount = await _context.CustomerFavorites.CountAsync(x => x.UserId == user.Id, cancellationToken);
        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), favoritesCount);
        DriverOperationalStatusDto? driverStatus = null;

        if (user.Role == UserRole.Driver)
        {
            var driver = await _context.Drivers
                .Include(d => d.PrimaryZone)
                .FirstOrDefaultAsync(d => d.UserId == user.Id, cancellationToken);

            if (driver is not null)
            {
                driverStatus = DriverOperationalStatusFactory.Create(driver);
            }
        }

        return new AuthResponseDto(tokens, userDto, DriverStatus: driverStatus);
    }

    public async Task<TokenPairDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await _refreshTokenStore.GetByTokenWithUserAsync(refreshToken, cancellationToken);

        if (tokenEntity == null || !tokenEntity.IsActive || tokenEntity.User == null)
        {
            throw new UnauthorizedException(_localizer["InvalidRefreshToken"]);
        }

        if (tokenEntity.User.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(_localizer["UserAccountNotActive"]);
        }

        if (tokenEntity.User.IsLoginLocked)
        {
            throw new UnauthorizedException(_localizer["UserAccountNotActive"]);
        }

        if (RequiresVerifiedCustomerEmail(tokenEntity.User))
        {
            throw new BusinessRuleException("ACCOUNT_EMAIL_NOT_VERIFIED", _localizer["AccountEmailNotVerified"]);
        }

        var newTokens = await _jwtTokenService.GenerateTokenPairAsync(tokenEntity.User, cancellationToken);
        await _refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(
            tokenEntity.UserId,
            newTokens.RefreshToken,
            DateTime.UtcNow.AddDays(7)
        ));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return newTokens;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await _refreshTokenStore.GetByTokenAsync(refreshToken, cancellationToken);
        if (tokenEntity != null && tokenEntity.IsActive)
        {
            await _refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var user = await _identityAccountService.FindByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            throw new UnauthorizedException(_localizer["UserNotFound"]);
        }

        var recordActivityResult = await _identityAccountService.RecordActivityAsync(user.Id, cancellationToken);
        if (!recordActivityResult.Succeeded)
        {
            var errors = string.Join(", ", recordActivityResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var favoritesCount = await _context.CustomerFavorites.CountAsync(x => x.UserId == user.Id, cancellationToken);
        return new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), favoritesCount);
    }

    private static bool RequiresVerifiedCustomerEmail(IdentityAccountSnapshot user) =>
        user.Role == UserRole.Customer &&
        !string.IsNullOrWhiteSpace(user.Email) &&
        !user.EmailConfirmed;
}
