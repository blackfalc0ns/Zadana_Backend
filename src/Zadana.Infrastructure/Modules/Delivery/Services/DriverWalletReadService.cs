using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Application.Modules.Wallets.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public sealed class DriverWalletReadService : IDriverWalletReadService
{
    private readonly IApplicationDbContext _context;
    private readonly IDriverRepository _driverRepository;

    public DriverWalletReadService(
        IApplicationDbContext context,
        IDriverRepository driverRepository)
    {
        _context = context;
        _driverRepository = driverRepository;
    }

    public async Task<DriverWalletSummaryDto> GetWalletSummaryAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(driverUserId, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(driver.Id, cancellationToken);

        var todayStart = SaudiTime.StartOfTodayUtc;
        var weekStart = DateTime.UtcNow.AddDays(-7);
        var monthStart = DateTime.UtcNow.AddDays(-30);

        var todayEarnings = await SumIncomingAsync(wallet.Id, todayStart, cancellationToken);
        var weekEarnings = await SumIncomingAsync(wallet.Id, weekStart, cancellationToken);
        var monthEarnings = await SumIncomingAsync(wallet.Id, monthStart, cancellationToken);

        var recentTransactions = await _context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(10)
            .Select(MapTransaction())
            .ToListAsync(cancellationToken);

        var paymentMethods = await _context.DriverPayoutMethods
            .AsNoTracking()
            .Where(m => m.DriverId == driver.Id)
            .OrderByDescending(m => m.IsPrimary)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Select(MapPayoutMethod())
            .ToListAsync(cancellationToken);

        var withdrawalSummary = await BuildWithdrawalSummaryAsync(driver.Id, cancellationToken);
        var activeWithdrawalHolds = await SumActiveWithdrawalHoldsAsync(driver.Id, cancellationToken);
        var pendingBalance = wallet.PendingBalance + activeWithdrawalHolds;
        var netWithdrawable = Math.Max(0m, wallet.CurrentBalance - wallet.CodOwedBalance - pendingBalance);

        return new DriverWalletSummaryDto(
            wallet.CurrentBalance,
            netWithdrawable,
            pendingBalance,
            wallet.CodOwedBalance,
            netWithdrawable,
            todayEarnings,
            weekEarnings,
            monthEarnings,
            recentTransactions,
            paymentMethods,
            withdrawalSummary,
            driver.PayoutDay.ToString());
    }

    public async Task<DriverWalletRealtimePayload> GetRealtimePayloadAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(driverUserId, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(driver.Id, cancellationToken);
        var recentTransactions = await _context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(3)
            .Select(MapTransaction())
            .ToListAsync(cancellationToken);

        var withdrawalSummary = await BuildWithdrawalSummaryAsync(driver.Id, cancellationToken);

        return new DriverWalletRealtimePayload(
            wallet.CurrentBalance,
            wallet.PendingBalance + await SumActiveWithdrawalHoldsAsync(driver.Id, cancellationToken),
            withdrawalSummary,
            recentTransactions);
    }

    private async Task<Domain.Modules.Delivery.Entities.Driver> GetDriverAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return await _driverRepository.GetByUserIdAsync(driverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", driverUserId);
    }

    private async Task<Wallet> GetOrCreateWalletAsync(Guid driverId, CancellationToken cancellationToken)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(
                w => w.OwnerType == WalletOwnerType.Driver && w.OwnerId == driverId,
                cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet(WalletOwnerType.Driver, driverId);
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private async Task<decimal> SumIncomingAsync(
        Guid walletId,
        DateTime fromUtc,
        CancellationToken cancellationToken)
    {
        return await _context.WalletTransactions
            .Where(t => t.WalletId == walletId && t.Direction == "IN" && t.CreatedAtUtc >= fromUtc)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
    }

    private async Task<DriverWithdrawalSummaryDto> BuildWithdrawalSummaryAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var query = _context.DriverWithdrawalRequests
            .AsNoTracking()
            .Where(w => w.DriverId == driverId);

        var pendingQuery = query.Where(w =>
            w.Status == DriverWithdrawalStatus.Pending ||
            w.Status == DriverWithdrawalStatus.Processing);

        var pendingCount = await pendingQuery.CountAsync(cancellationToken);
        var pendingAmount = await pendingQuery.SumAsync(w => (decimal?)w.Amount, cancellationToken) ?? 0m;
        var totalRequests = await query.CountAsync(cancellationToken);

        return new DriverWithdrawalSummaryDto(pendingCount, pendingAmount, totalRequests);
    }

    private async Task<decimal> SumActiveWithdrawalHoldsAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        return await _context.WalletHolds
            .AsNoTracking()
            .Where(item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == driverId &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.Status == WalletHoldStatus.Active)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
    }

    private static Expression<Func<WalletTransaction, DriverWalletTransactionDto>> MapTransaction() =>
        item => new DriverWalletTransactionDto(
            item.Id,
            item.TxnType.ToString(),
            item.Direction,
            item.Amount,
            item.Description,
            item.ReferenceType,
            item.ReferenceId.HasValue ? item.ReferenceId.Value.ToString() : null,
            item.CreatedAtUtc);

    private static Expression<Func<DriverPayoutMethod, DriverPayoutMethodDto>> MapPayoutMethod() =>
        item => new DriverPayoutMethodDto(
            item.Id,
            item.MethodType.ToString(),
            item.AccountHolderName,
            item.ProviderName,
            item.MaskedLabel,
            item.IsPrimary,
            item.IsVerified);
}
