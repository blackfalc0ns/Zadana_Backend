using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
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
    private readonly IAccessControlService _accessControlService;
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IJwtRevocationStore _jwtRevocationStore;

    public IdentityService(
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService,
        IAccessControlService accessControlService,
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer,
        IJwtRevocationStore jwtRevocationStore)
    {
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
        _accessControlService = accessControlService;
        _context = context;
        _localizer = localizer;
        _jwtRevocationStore = jwtRevocationStore;
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

        if (user.IsLoginLocked)
        {
            if (user.Role == UserRole.Driver)
            {
                throw new UnauthorizedException(
                    "DRIVER_LOGIN_LOCKED",
                    "DRIVER_LOGIN_LOCKED");
            }

            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            if (user.Role == UserRole.Driver)
            {
                throw new UnauthorizedException(
                    "DRIVER_ACCOUNT_BLOCKED",
                    "DRIVER_ACCOUNT_BLOCKED");
            }

            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        EnsureEmailVerified(user);

        user = await EnsureDriverAccessScopeAsync(user, cancellationToken);

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
        var access = await _accessControlService.GetEffectiveAccessAsync(user.Id, cancellationToken);
        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.MustChangePassword, favoritesCount, access, user.ProfilePhotoUrl);
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

        var isVerified = AuthResponseVerificationResolver.Resolve(user, driverStatus);

        return new AuthResponseDto(tokens, userDto, IsVerified: isVerified, DriverStatus: driverStatus);
    }

    public async Task<TokenPairDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await _refreshTokenStore.GetByTokenWithUserAsync(refreshToken, cancellationToken);

        if (tokenEntity == null || tokenEntity.User == null)
        {
            throw new UnauthorizedException(_localizer["InvalidRefreshToken"]);
        }

        // Reuse detection: if a token that was already revoked is presented
        // again, treat it as a stolen token and burn every active refresh
        // token for that user. The legitimate user will be forced to re-login,
        // but the attacker (or stolen device) loses access immediately.
        if (tokenEntity.IsRevoked)
        {
            await _refreshTokenStore.RevokeAllByUserAsync(tokenEntity.UserId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Also blanket-revoke every JWT this user holds. The
            // JwtRevocationMiddleware will reject any access token issued at
            // or before this moment, so the attacker can't keep using a
            // valid-but-stolen access token until its natural expiry.
            await _jwtRevocationStore.RevokeAllForUserAsync(
                tokenEntity.UserId,
                DateTime.UtcNow,
                cancellationToken);

            throw new UnauthorizedException(_localizer["InvalidRefreshToken"]);
        }

        if (!tokenEntity.IsActive)
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

        EnsureEmailVerified(tokenEntity.User);

        var refreshedUser = tokenEntity.User.Role == UserRole.Driver
            ? await EnsureDriverAccessScopeAsync(tokenEntity.User, cancellationToken)
            : tokenEntity.User;

        var newTokens = await _jwtTokenService.GenerateTokenPairAsync(refreshedUser, cancellationToken);
        await _refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(
            refreshedUser.Id,
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

        // Also revoke the in-flight access token (the JTI is on the current
        // claims principal). This stops a leaked access token from outliving
        // a logout call up to its natural expiry.
        var jti = _currentUserService.AccessTokenJti;
        if (!string.IsNullOrEmpty(jti) && _currentUserService.AccessTokenExpiresAtUtc.HasValue)
        {
            await _jwtRevocationStore.RevokeAsync(jti, _currentUserService.AccessTokenExpiresAtUtc.Value, cancellationToken);
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

        EnsureEmailVerified(user);

        var recordActivityResult = await _identityAccountService.RecordActivityAsync(user.Id, cancellationToken);
        if (!recordActivityResult.Succeeded)
        {
            var errors = string.Join(", ", recordActivityResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var favoritesCount = await _context.CustomerFavorites.CountAsync(x => x.UserId == user.Id, cancellationToken);
        var access = await _accessControlService.GetEffectiveAccessAsync(user.Id, cancellationToken);
        return new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.MustChangePassword, favoritesCount, access, user.ProfilePhotoUrl);
    }

    private void EnsureEmailVerified(IdentityAccountSnapshot user)
    {
        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException(_localizer["AccountEmailNotVerified"]);
        }
    }

    private async Task<IdentityAccountSnapshot> EnsureDriverAccessScopeAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken)
    {
        if (account.Role != UserRole.Driver)
        {
            return account;
        }

        var driverProjection = await _context.Drivers
            .AsNoTracking()
            .Where(driver => driver.UserId == account.Id)
            .Select(driver => new { driver.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (driverProjection is null)
        {
            return account;
        }

        var driverRole = await _context.RoleDefinitions
            .AsNoTracking()
            .Where(role => role.Code == "driver_account" && role.IsActive)
            .Select(role => new { role.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (driverRole is null)
        {
            return account;
        }

        var existingScope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(scope => scope.UserId == account.Id && scope.IsActive, cancellationToken);

        var changed = false;

        if (existingScope is null)
        {
            _context.UserAccessScopes.Add(new UserAccessScope(
                account.Id,
                driverRole.Id,
                PanelScope.DriverApp,
                AccessScopeType.DriverSelf,
                driverProjection.Id));
            changed = true;
        }
        else if (existingScope.RoleDefinitionId != driverRole.Id ||
                 existingScope.PanelScope != PanelScope.DriverApp ||
                 existingScope.ScopeType != AccessScopeType.DriverSelf ||
                 existingScope.ScopeEntityId != driverProjection.Id)
        {
            existingScope.Update(
                driverRole.Id,
                PanelScope.DriverApp,
                AccessScopeType.DriverSelf,
                driverProjection.Id,
                null);
            changed = true;
        }

        if (!changed)
        {
            return account;
        }

        var userEntity = await _context.Users.FirstOrDefaultAsync(user => user.Id == account.Id, cancellationToken);
        if (userEntity is null)
        {
            return account;
        }

        userEntity.IncrementPermissionVersion();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdentityAccountSnapshot(
            userEntity.Id,
            userEntity.FullName,
            userEntity.Email,
            userEntity.PhoneNumber,
            userEntity.Role,
            userEntity.PermissionVersion,
            userEntity.AccountStatus,
            userEntity.IsLoginLocked,
            userEntity.LockedAtUtc,
            userEntity.ArchivedAtUtc,
            userEntity.EmailConfirmed,
            userEntity.PhoneNumberConfirmed,
            userEntity.MustChangePassword,
            userEntity.ProfilePhotoUrl);
    }
}
