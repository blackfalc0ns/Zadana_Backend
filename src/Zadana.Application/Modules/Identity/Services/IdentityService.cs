using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<User> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public IdentityService(
        UserManager<User> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> LoginAsync(string identifier, string password, UserRole[]? expectedRoles = null, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == identifier || u.PhoneNumber == identifier, cancellationToken);
            
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedException(_localizer["InvalidCredentials"]);
        }

        if (expectedRoles != null && expectedRoles.Length > 0 && !expectedRoles.Contains(user.Role))
        {
            throw new UnauthorizedException(_localizer["UnauthorizedAppAccess"]);
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);
        
        var refreshTokenEntity = new RefreshToken(
            user.Id,
            tokens.RefreshToken, 
            DateTime.UtcNow.AddDays(7)
        );
        
        _refreshTokenRepository.Add(refreshTokenEntity);
        user.RecordLogin();
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }

    public async Task<TokenPairDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await _refreshTokenRepository.GetByTokenWithUserAsync(refreshToken, cancellationToken);

        if (tokenEntity == null || !tokenEntity.IsActive)
        {
            throw new UnauthorizedException(_localizer["InvalidRefreshToken"]);
        }

        if (tokenEntity.User.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(_localizer["UserAccountNotActive"]);
        }

        var newTokens = await _jwtTokenService.GenerateTokenPairAsync(tokenEntity.User, cancellationToken);
        tokenEntity.Revoke();

        var newRefreshTokenEntity = new RefreshToken(
            tokenEntity.UserId,
            newTokens.RefreshToken,
            DateTime.UtcNow.AddDays(7)
        );

        _refreshTokenRepository.Add(newRefreshTokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return newTokens;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken, cancellationToken);

        if (tokenEntity != null && tokenEntity.IsActive)
        {
            tokenEntity.Revoke();
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

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedException(_localizer["UserNotFound"]);
        }

        return new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
    }
}
