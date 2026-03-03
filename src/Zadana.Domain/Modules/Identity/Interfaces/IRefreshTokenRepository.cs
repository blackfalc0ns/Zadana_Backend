using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Domain.Modules.Identity.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    void Add(RefreshToken refreshToken);
}
