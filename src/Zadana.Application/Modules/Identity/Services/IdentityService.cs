using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

public class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUserService;

    public IdentityService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
    }

    public async Task<AuthResponseDto> LoginAsync(string identifier, string password, UserRole[]? expectedRoles = null, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdentifierAsync(identifier, cancellationToken);
            
        if (user == null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid credentials.");
        }

        if (expectedRoles != null && expectedRoles.Length > 0 && !expectedRoles.Contains(user.Role))
        {
            throw new UnauthorizedException("غير مصرح لك بالدخول إلى هذا التطبيق.");
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException($"Account login denied. Status is {user.AccountStatus}.");
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

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }

    public async Task<TokenPairDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await _refreshTokenRepository.GetByTokenWithUserAsync(refreshToken, cancellationToken);

        if (tokenEntity == null || !tokenEntity.IsActive)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        if (tokenEntity.User.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException("User account is no longer active.");
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
            throw new UnauthorizedException("User is not authenticated.");
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            throw new UnauthorizedException("User not found.");
        }

        return new CurrentUserDto(user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString());
    }
}
