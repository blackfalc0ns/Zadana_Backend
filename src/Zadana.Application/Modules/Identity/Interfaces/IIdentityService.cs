using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IIdentityService
{
    Task<AuthResponseDto> LoginAsync(string identifier, string password, UserRole[]? expectedRoles = null, CancellationToken cancellationToken = default);
    Task<TokenPairDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
