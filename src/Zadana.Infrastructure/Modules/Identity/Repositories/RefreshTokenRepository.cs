using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Identity.Repositories;

public class RefreshTokenRepository : IRefreshTokenStore
{
    private readonly ApplicationDbContext _dbContext;

    public RefreshTokenRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RefreshTokenRecord?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

        return refreshToken == null ? null : Map(refreshToken);
    }

    public async Task<RefreshTokenRecord?> GetByTokenWithUserAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

        return refreshToken == null ? null : Map(refreshToken, includeUser: true);
    }

    public void Add(NewRefreshToken refreshToken)
    {
        _dbContext.RefreshTokens.Add(new RefreshToken(refreshToken.UserId, refreshToken.Token, refreshToken.ExpiresAtUtc));
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

        if (refreshToken == null)
        {
            return false;
        }

        refreshToken.Revoke();
        return true;
    }

    private static RefreshTokenRecord Map(RefreshToken refreshToken, bool includeUser = false) =>
        new(
            refreshToken.UserId,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc,
            refreshToken.IsRevoked,
            refreshToken.RevokedAtUtc,
            includeUser
                ? new IdentityAccountSnapshot(
                    refreshToken.User.Id,
                    refreshToken.User.FullName,
                    refreshToken.User.Email,
                    refreshToken.User.PhoneNumber,
                    refreshToken.User.Role,
                    refreshToken.User.AccountStatus,
                    refreshToken.User.EmailConfirmed,
                    refreshToken.User.PhoneNumberConfirmed)
                : null);
}
