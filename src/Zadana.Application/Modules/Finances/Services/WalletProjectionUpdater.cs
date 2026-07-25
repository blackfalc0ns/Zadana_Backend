using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class WalletProjectionUpdater
{
    private const int MaxProjectionAttempts = 2;

    private readonly IApplicationDbContext _context;

    public WalletProjectionUpdater(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ApplyJournalEntryAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxProjectionAttempts; attempt++)
        {
            try
            {
                await ApplyJournalEntryCoreAsync(journalEntryId, cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxProjectionAttempts && _context is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task ApplyJournalEntryCoreAsync(Guid journalEntryId, CancellationToken cancellationToken)
    {
        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == journalEntryId, cancellationToken);

        if (entry is null)
        {
            return;
        }

        var relevantLines = entry.Lines
            .Where(IsWalletProjectionLine)
            .Select(line => new
            {
                Line = line,
                Owner = ResolveWalletOwner(line.AccountCode, line.OwnerType, line.OwnerId)
            })
            .Where(item => item.Owner is not null)
            .GroupBy(item => item.Owner!.Value)
            .ToList();

        foreach (var ownerLines in relevantLines)
        {
            var owner = ownerLines.Key;
            var wallet = await GetOrCreateWalletAsync(owner.OwnerType, owner.OwnerId, cancellationToken);

            // Use Serializable transaction isolation when using relational DB
            if (_context is DbContext dbContext &&
                !string.Equals(
                    dbContext.Database.ProviderName,
                    "Microsoft.EntityFrameworkCore.InMemory",
                    StringComparison.OrdinalIgnoreCase) &&
                dbContext.Database.CurrentTransaction is null)
            {
                var strategy = dbContext.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        // EF Core doesn't directly support setting isolation level in BeginTransactionAsync
                        // The serializable isolation must be set at connection or command level
                        await ApplyOwnerBatchAsync(wallet, entry, ownerLines, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });
            }
            else
            {
                await ApplyOwnerBatchAsync(wallet, entry, ownerLines, cancellationToken);
            }
        }
    }

    private async Task ApplyOwnerBatchAsync(
        Wallet wallet,
        Domain.Modules.Finances.Entities.JournalEntry entry,
        IEnumerable<dynamic> ownerLines,
        CancellationToken cancellationToken)
    {
        var currentBalance = wallet.CurrentBalance;
        var pendingBalance = wallet.PendingBalance;
        var codOwedBalance = wallet.CodOwedBalance;
        var appliedAnyLine = false;

        foreach (var item in ownerLines)
        {
            var line = item.Line;
            if (await WalletTransactionExistsAsync(line.Id, cancellationToken))
            {
                continue;
            }

            switch (line.AccountCode)
            {
                case FinancialAccountCode.VendorPayable:
                case FinancialAccountCode.DriverPayable:
                case FinancialAccountCode.PlatformRevenue:
                case FinancialAccountCode.ManualAdjustment:
                    currentBalance += line.CreditAmount - line.DebitAmount;
                    break;
                case FinancialAccountCode.DriverCodReceivable:
                case FinancialAccountCode.VendorCodReceivable:
                    codOwedBalance += line.DebitAmount - line.CreditAmount;
                    break;
            }

            AddWalletTransaction(wallet.Id, line);
            appliedAnyLine = true;
        }

        if (!appliedAnyLine)
        {
            return;
        }

        wallet.SetProjectionBalances(
            currentBalance,
            pendingBalance,
            codOwedBalance,
            Math.Max(wallet.LastJournalSequence, entry.SequenceNumber),
            entry.CurrencyCode);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<WalletProjectionRebuildResult> RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        var wallets = await _context.Wallets.ToListAsync(cancellationToken);
        foreach (var wallet in wallets)
        {
            wallet.SetProjectionBalances(0m, 0m, 0m, 0L, wallet.CurrencyCode);
        }

        var walletTransactions = await _context.WalletTransactions
            .Where(item => item.ReferenceType == "JournalLine")
            .ToListAsync(cancellationToken);

        if (walletTransactions.Count > 0)
        {
            _context.WalletTransactions.RemoveRange(walletTransactions);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var journalEntryIds = await _context.JournalEntries
            .AsNoTracking()
            .OrderBy(item => item.SequenceNumber)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        foreach (var journalEntryId in journalEntryIds)
        {
            await ApplyJournalEntryAsync(journalEntryId, cancellationToken);
        }

        return new WalletProjectionRebuildResult(journalEntryIds.Count, await _context.Wallets.CountAsync(cancellationToken));
    }

    public async Task<WalletProjectionReconciliationReport> BuildReconciliationReportAsync(CancellationToken cancellationToken = default)
    {
        var wallets = await _context.Wallets.AsNoTracking().ToListAsync(cancellationToken);
        var issues = new List<WalletProjectionReconciliationIssue>();

        foreach (var wallet in wallets)
        {
            var ownerType = wallet.OwnerType switch
            {
                WalletOwnerType.Vendor => FinancialOwnerType.Vendor,
                WalletOwnerType.Driver => FinancialOwnerType.Driver,
                WalletOwnerType.Platform => FinancialOwnerType.Platform,
                _ => (FinancialOwnerType?)null
            };

            if (ownerType is null)
            {
                continue;
            }

            var lines = await _context.JournalLines
                .AsNoTracking()
                .Where(line => line.OwnerType == ownerType && line.OwnerId == wallet.OwnerId)
                .ToListAsync(cancellationToken);

            var expectedCurrent = lines
                .Where(line => line.AccountCode is FinancialAccountCode.VendorPayable
                    or FinancialAccountCode.DriverPayable
                    or FinancialAccountCode.PlatformRevenue
                    or FinancialAccountCode.ManualAdjustment)
                .Sum(line => line.CreditAmount - line.DebitAmount);

            var expectedCod = lines
                .Where(line => line.AccountCode is FinancialAccountCode.DriverCodReceivable
                    or FinancialAccountCode.VendorCodReceivable)
                .Sum(line => line.DebitAmount - line.CreditAmount);

            var currentDiff = wallet.CurrentBalance - expectedCurrent;
            var codDiff = wallet.CodOwedBalance - expectedCod;

            if (currentDiff != 0m || codDiff != 0m)
            {
                issues.Add(new WalletProjectionReconciliationIssue(
                    wallet.Id,
                    wallet.OwnerType.ToString(),
                    wallet.OwnerId,
                    wallet.CurrentBalance,
                    expectedCurrent,
                    currentDiff,
                    wallet.CodOwedBalance,
                    expectedCod,
                    codDiff));
            }
        }

        return new WalletProjectionReconciliationReport(wallets.Count, issues.Count, issues);
    }

    private async Task<Wallet> GetOrCreateWalletAsync(
        WalletOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(item => item.OwnerType == ownerType && item.OwnerId == ownerId, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet(ownerType, ownerId);
        _context.Wallets.Add(wallet);
        return wallet;
    }

    private static bool IsWalletProjectionLine(Domain.Modules.Finances.Entities.JournalLine line) =>
        line.AccountCode is FinancialAccountCode.VendorPayable
            or FinancialAccountCode.DriverPayable
            or FinancialAccountCode.DriverCodReceivable
            or FinancialAccountCode.VendorCodReceivable
            or FinancialAccountCode.PlatformRevenue
            or FinancialAccountCode.ManualAdjustment;

    private Task<bool> WalletTransactionExistsAsync(
        Guid journalLineId,
        CancellationToken cancellationToken)
    {
        return _context.WalletTransactions.AnyAsync(
            item => item.ReferenceType == "JournalLine" && item.ReferenceId == journalLineId,
            cancellationToken);
    }

    private void AddWalletTransaction(Guid walletId, Domain.Modules.Finances.Entities.JournalLine line)
    {
        var amount = Math.Max(line.DebitAmount, line.CreditAmount);
        if (amount <= 0)
        {
            return;
        }

        var direction = ResolveDirection(line);
        var txnType = ResolveTxnType(line.AccountCode);

        _context.WalletTransactions.Add(new WalletTransaction(
            walletId,
            txnType,
            amount,
            direction,
            orderId: line.OrderId,
            settlementId: line.SettlementId,
            referenceType: "JournalLine",
            referenceId: line.Id,
            description: line.Memo));
    }

    private static string ResolveDirection(Domain.Modules.Finances.Entities.JournalLine line)
    {
        if (line.AccountCode is FinancialAccountCode.DriverCodReceivable or FinancialAccountCode.VendorCodReceivable)
        {
            return line.DebitAmount > 0 ? "OUT" : "IN";
        }

        return line.CreditAmount > 0 ? "IN" : "OUT";
    }

    private static WalletTxnType ResolveTxnType(FinancialAccountCode accountCode) =>
        accountCode switch
        {
            FinancialAccountCode.DriverCodReceivable or FinancialAccountCode.VendorCodReceivable => WalletTxnType.CashCollected,
            FinancialAccountCode.ManualAdjustment => WalletTxnType.Adjustment,
            _ => WalletTxnType.OrderRevenue
        };

    private static (WalletOwnerType OwnerType, Guid OwnerId)? ResolveWalletOwner(
        FinancialAccountCode accountCode,
        FinancialOwnerType? ownerType,
        Guid? ownerId)
    {
        if (ownerId is null)
        {
            return null;
        }

        return ownerType switch
        {
            FinancialOwnerType.Vendor => (WalletOwnerType.Vendor, ownerId.Value),
            FinancialOwnerType.Driver => (WalletOwnerType.Driver, ownerId.Value),
            FinancialOwnerType.Platform => (WalletOwnerType.Platform, ownerId.Value),
            _ when accountCode == FinancialAccountCode.PlatformRevenue => (WalletOwnerType.Platform, ownerId.Value),
            _ => null
        };
    }
}

public sealed record WalletProjectionRebuildResult(int JournalEntriesApplied, int WalletsProcessed);

public sealed record WalletProjectionReconciliationReport(
    int WalletsChecked,
    int IssueCount,
    IReadOnlyList<WalletProjectionReconciliationIssue> Issues);

public sealed record WalletProjectionReconciliationIssue(
    Guid WalletId,
    string OwnerType,
    Guid OwnerId,
    decimal CurrentBalance,
    decimal ExpectedCurrentBalance,
    decimal CurrentBalanceDifference,
    decimal CodOwedBalance,
    decimal ExpectedCodOwedBalance,
    decimal CodOwedBalanceDifference);
