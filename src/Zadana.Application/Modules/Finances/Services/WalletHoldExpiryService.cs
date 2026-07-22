using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Processes expired wallet holds by changing their status from Active to Expired.
/// </summary>
public sealed class WalletHoldExpiryService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<WalletHoldExpiryService> _logger;

    public WalletHoldExpiryService(
        IApplicationDbContext context,
        ILogger<WalletHoldExpiryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Expires all Active holds where ExpiresAtUtc is less than the current UTC time.
    /// </summary>
    public async Task<int> ExpireOverdueHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var overdueHolds = await _context.WalletHolds
            .Where(hold =>
                hold.Status == WalletHoldStatus.Active &&
                hold.ExpiresAtUtc.HasValue &&
                hold.ExpiresAtUtc.Value < now)
            .ToListAsync(cancellationToken);

        if (overdueHolds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation(
            "[WalletHoldExpiry] Found {Count} overdue holds to expire.",
            overdueHolds.Count);

        foreach (var hold in overdueHolds)
        {
            try
            {
                hold.Expire();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[WalletHoldExpiry] Failed to expire hold {HoldId} for owner {OwnerType} {OwnerId}.",
                    hold.Id,
                    hold.OwnerType,
                    hold.OwnerId);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[WalletHoldExpiry] Expired {Count} overdue holds.",
            overdueHolds.Count);

        return overdueHolds.Count;
    }
}
