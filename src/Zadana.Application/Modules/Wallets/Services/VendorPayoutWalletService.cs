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

        var wallet = await GetOrCreateVendorWalletAsync(vendorId, cancellationToken);
        var exists = await _context.WalletHolds.AnyAsync(
            hold =>
                hold.OwnerType == WalletOwnerType.Vendor &&
                hold.OwnerId == vendorId &&
                hold.Reason == WalletHoldReason.Payout &&
                hold.ReferenceType == "Settlement" &&
                hold.ReferenceId == settlementId &&
                hold.Status == WalletHoldStatus.Active,
            cancellationToken);

        if (exists)
        {
            return;
        }

        _context.WalletHolds.Add(new WalletHold(
            WalletOwnerType.Vendor,
            vendorId,
            amount,
            WalletHoldReason.Payout,
            $"vendor-payout-hold:{vendorId:N}:{settlementId:N}",
            walletId: wallet.Id,
            referenceType: "Settlement",
            referenceId: settlementId,
            memo: description));
    }

    public async Task ReleaseHoldAsync(
        Guid vendorId,
        Guid settlementId,
        decimal amount,
        string referenceType,
        string description,
        CancellationToken cancellationToken)
    {
        var holds = await LoadActiveSettlementHoldsAsync(vendorId, settlementId, cancellationToken);
        foreach (var hold in holds)
        {
            hold.Release(description);
        }
    }

    public async Task SettleHoldAsync(
        Guid vendorId,
        Guid settlementId,
        Guid payoutId,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        var holds = await LoadActiveSettlementHoldsAsync(vendorId, settlementId, cancellationToken);
        foreach (var hold in holds)
        {
            hold.Consume();
        }
    }

    public async Task<Guid?> RecoverFromHoldAsync(
        Guid vendorId,
        Guid settlementId,
        Guid recoveryId,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        var wallet = await GetOrCreateVendorWalletAsync(vendorId, cancellationToken);
        var exists = await _context.WalletTransactions.AnyAsync(
            txn => txn.ReferenceType == "VendorHoldRecovery" && txn.ReferenceId == recoveryId,
            cancellationToken);

        if (exists)
        {
            return null;
        }

        var transaction = new WalletTransaction(
            wallet.Id,
            WalletTxnType.Debit,
            amount,
            "OUT",
            settlementId: settlementId,
            referenceType: "VendorHoldRecovery",
            referenceId: recoveryId,
            description: description);

        _context.WalletTransactions.Add(transaction);
        return transaction.Id;
    }

    private async Task<Wallet> GetOrCreateVendorWalletAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(
            item => item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == vendorId,
            cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet(WalletOwnerType.Vendor, vendorId);
        _context.Wallets.Add(wallet);
        return wallet;
    }

    private Task<List<WalletHold>> LoadActiveSettlementHoldsAsync(
        Guid vendorId,
        Guid settlementId,
        CancellationToken cancellationToken) =>
        _context.WalletHolds
            .Where(hold =>
                hold.OwnerType == WalletOwnerType.Vendor &&
                hold.OwnerId == vendorId &&
                hold.Reason == WalletHoldReason.Payout &&
                hold.ReferenceType == "Settlement" &&
                hold.ReferenceId == settlementId &&
                hold.Status == WalletHoldStatus.Active)
            .ToListAsync(cancellationToken);

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
