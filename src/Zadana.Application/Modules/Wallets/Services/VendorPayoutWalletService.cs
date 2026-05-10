using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Wallets.Services;

public class VendorPayoutWalletService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<VendorPayoutWalletService> _logger;

    public VendorPayoutWalletService(
        IApplicationDbContext context,
        ILogger<VendorPayoutWalletService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureHoldAsync(
        Guid vendorId,
        Guid settlementId,
        decimal amount,
        string referenceType,
        string description,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task ReleaseHoldAsync(
        Guid vendorId,
        Guid settlementId,
        decimal amount,
        string referenceType,
        string description,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task SettleHoldAsync(
        Guid vendorId,
        Guid settlementId,
        Guid payoutId,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task<Guid?> RecoverFromHoldAsync(
        Guid vendorId,
        Guid settlementId,
        Guid recoveryId,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return null;
    }

    private Task<Wallet?> GetVendorWalletAsync(Guid vendorId, CancellationToken cancellationToken) =>
        _context.Wallets.FirstOrDefaultAsync(
            wallet => wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendorId,
            cancellationToken);

    private Task<bool> HasSettlementTxnAsync(
        Guid walletId,
        Guid settlementId,
        WalletTxnType txnType,
        CancellationToken cancellationToken) =>
        _context.WalletTransactions.AnyAsync(
            txn =>
                txn.WalletId == walletId &&
                txn.SettlementId == settlementId &&
                txn.TxnType == txnType,
            cancellationToken);
}
