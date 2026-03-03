using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Identity.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RefreshTokenRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
    }

    public async Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        _dbContext.RefreshTokens.Add(refreshToken);
    }
}
