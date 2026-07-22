using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class DriverCodEnforcementService
{
    private readonly IApplicationDbContext _context;
    private readonly FinancialSettingsOptions _settings;

    public DriverCodEnforcementService(
        IApplicationDbContext context,
        IOptions<FinancialSettingsOptions> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    public async Task<IReadOnlySet<Guid>> GetBlockedDriverIdsAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default)
    {
        if (driverIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var blocked = await _context.Wallets
            .AsNoTracking()
            .Where(wallet =>
                wallet.OwnerType == WalletOwnerType.Driver &&
                driverIds.Contains(wallet.OwnerId) &&
                wallet.CodOwedBalance >= _settings.DriverCodBlockThresholdAmount)
            .Select(wallet => wallet.OwnerId)
            .ToListAsync(cancellationToken);

        return blocked.ToHashSet();
    }

    public async Task<bool> IsDriverBlockedAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var codOwedBalance = await GetCodOwedBalanceAsync(driverId, cancellationToken);
        return codOwedBalance >= _settings.DriverCodBlockThresholdAmount;
    }

    public async Task<decimal> GetCodOwedBalanceAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Wallets
            .AsNoTracking()
            .Where(wallet =>
                wallet.OwnerType == WalletOwnerType.Driver &&
                wallet.OwnerId == driverId)
            .Select(wallet => (decimal?)wallet.CodOwedBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
    }

    public decimal BlockThresholdAmount => _settings.DriverCodBlockThresholdAmount;
}
