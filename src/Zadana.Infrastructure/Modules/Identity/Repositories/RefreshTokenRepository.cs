using System.Security.Cryptography;
using System.Text;
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
        var refreshToken = await FindByPlaintextOrHashAsync(token, includeUser: false, cancellationToken);
        return refreshToken == null ? null : Map(refreshToken);
    }

    public async Task<RefreshTokenRecord?> GetByTokenWithUserAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await FindByPlaintextOrHashAsync(token, includeUser: true, cancellationToken);
        return refreshToken == null ? null : Map(refreshToken, includeUser: true);
    }

    public void Add(NewRefreshToken refreshToken)
    {
        // New rows always store the hash, never the plaintext. The plaintext
        // is the value the caller passes back to us on the next refresh; we
        // only need to recognize it.
        var hash = HashToken(refreshToken.Token);
        _dbContext.RefreshTokens.Add(RefreshToken.CreateHashed(refreshToken.UserId, hash, refreshToken.ExpiresAtUtc));
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await FindByPlaintextOrHashAsync(token, includeUser: false, cancellationToken);
        if (refreshToken == null)
        {
            return false;
        }

        refreshToken.Revoke();
        return true;
    }

    public async Task<int> RevokeAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(item => item.UserId == userId && !item.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        return tokens.Count;
    }

    private async Task<RefreshToken?> FindByPlaintextOrHashAsync(string token, bool includeUser, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = HashToken(token);

        var query = includeUser
            ? _dbContext.RefreshTokens.Include(rt => rt.User).AsQueryable()
            : _dbContext.RefreshTokens.AsQueryable();

        // Match by hash (new rows) OR by plaintext (legacy rows). Legacy rows
        // remain functional until they expire / are rotated naturally.
        return await query
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash || rt.Token == token, cancellationToken);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static RefreshTokenRecord Map(RefreshToken refreshToken, bool includeUser = false) =>
        new(
            refreshToken.UserId,
            // Surface the value the caller originally passed; we don't have
            // the plaintext stored anymore for new rows, so return the hash
            // identifier. Callers only use this string for revoke lookups
            // which run through FindByPlaintextOrHashAsync.
            refreshToken.Token ?? refreshToken.TokenHash ?? string.Empty,
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
                    refreshToken.User.PermissionVersion,
                    refreshToken.User.AccountStatus,
                    refreshToken.User.IsLoginLocked,
                    refreshToken.User.LockedAtUtc,
                    refreshToken.User.ArchivedAtUtc,
                    refreshToken.User.EmailConfirmed,
                    refreshToken.User.PhoneNumberConfirmed,
                    refreshToken.User.MustChangePassword,
                    refreshToken.User.ProfilePhotoUrl)
                : null);
}
