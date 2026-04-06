using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IRefreshTokenStore
{
    Task<RefreshTokenRecord?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<RefreshTokenRecord?> GetByTokenWithUserAsync(string token, CancellationToken cancellationToken = default);
    void Add(NewRefreshToken refreshToken);
    Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default);
}
