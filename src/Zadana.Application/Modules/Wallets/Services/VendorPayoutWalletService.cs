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
        if (amount <= 0)
        {
            return;
        }

        var wallet = await GetVendorWalletAsync(vendorId, cancellationToken);
        if (wallet is null)
        {
            _logger.LogWarning(
                "[VendorPayoutWallet] Wallet not found while creating hold for vendor {VendorId}, settlement {SettlementId}.",
                vendorId,
                settlementId);
            return;
        }

        var holdAlreadyActive = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Hold,
            cancellationToken);

        var holdAlreadyReleased = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Release,
            cancellationToken);

        var holdAlreadySettled = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Payout,
            cancellationToken);

        if (holdAlreadyActive && !holdAlreadyReleased && !holdAlreadySettled)
        {
            return;
        }

        wallet.Hold(amount);
        _context.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTxnType.Hold,
            amount,
            "OUT",
            settlementId: settlementId,
            referenceType: referenceType,
            referenceId: settlementId,
            description: description));
    }

    public async Task ReleaseHoldAsync(
        Guid vendorId,
        Guid settlementId,
        decimal amount,
        string referenceType,
        string description,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return;
        }

        var wallet = await GetVendorWalletAsync(vendorId, cancellationToken);
        if (wallet is null)
        {
            return;
        }

        var holdRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Hold,
            cancellationToken);

        var releaseAlreadyRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Release,
            cancellationToken);

        var payoutAlreadyRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Payout,
            cancellationToken);

        if (!holdRecorded || releaseAlreadyRecorded || payoutAlreadyRecorded || wallet.PendingBalance < amount)
        {
            return;
        }

        wallet.ReleaseHold(amount);
        _context.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTxnType.Release,
            amount,
            "IN",
            settlementId: settlementId,
            referenceType: referenceType,
            referenceId: settlementId,
            description: description));
    }

    public async Task SettleHoldAsync(
        Guid vendorId,
        Guid settlementId,
        Guid payoutId,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return;
        }

        var wallet = await GetVendorWalletAsync(vendorId, cancellationToken);
        if (wallet is null)
        {
            return;
        }

        var holdRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Hold,
            cancellationToken);

        var payoutAlreadyRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Payout,
            cancellationToken);

        var releaseAlreadyRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Release,
            cancellationToken);

        if (!holdRecorded || payoutAlreadyRecorded || releaseAlreadyRecorded || wallet.PendingBalance < amount)
        {
            return;
        }

        wallet.SettleHold(amount);
        _context.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTxnType.Payout,
            amount,
            "OUT",
            settlementId: settlementId,
            referenceType: "VendorPayout",
            referenceId: payoutId,
            description: description));
    }

    public async Task<Guid?> RecoverFromHoldAsync(
        Guid vendorId,
        Guid settlementId,
        Guid recoveryId,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return null;
        }

        var wallet = await GetVendorWalletAsync(vendorId, cancellationToken);
        if (wallet is null)
        {
            return null;
        }

        var holdRecorded = await HasSettlementTxnAsync(
            wallet.Id,
            settlementId,
            WalletTxnType.Hold,
            cancellationToken);

        if (!holdRecorded || wallet.PendingBalance < amount)
        {
            return null;
        }

        wallet.SettleHold(amount);
        var txn = new WalletTransaction(
            wallet.Id,
            WalletTxnType.Debit,
            amount,
            "OUT",
            settlementId: settlementId,
            referenceType: "VendorRecovery",
            referenceId: recoveryId,
            description: description);
        _context.WalletTransactions.Add(txn);
        return txn.Id;
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
