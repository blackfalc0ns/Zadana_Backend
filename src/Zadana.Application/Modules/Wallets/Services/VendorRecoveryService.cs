using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Wallets.Services;

public class VendorRecoveryService
{
    private readonly IApplicationDbContext _context;
    private readonly VendorPayoutWalletService _vendorPayoutWalletService;

    public VendorRecoveryService(
        IApplicationDbContext context,
        VendorPayoutWalletService vendorPayoutWalletService)
    {
        _context = context;
        _vendorPayoutWalletService = vendorPayoutWalletService;
    }

    public async Task<VendorRecovery?> StageRecoveryForApprovedCaseAsync(
        OrderSupportCase supportCase,
        decimal approvedAmount,
        string? costBearer,
        CancellationToken cancellationToken = default)
    {
        if (supportCase.Type != OrderSupportCaseType.ReturnRequest)
        {
            return null;
        }

        var vendorResponsibilityAmount = ResolveVendorResponsibilityAmount(approvedAmount, costBearer);
        if (vendorResponsibilityAmount <= 0m)
        {
            return null;
        }

        var existingRecovery = await _context.VendorRecoveries
            .FirstOrDefaultAsync(item => item.OrderSupportCaseId == supportCase.Id, cancellationToken);

        var recovery = existingRecovery ?? new VendorRecovery(
            supportCase.Order.VendorId,
            supportCase.OrderId,
            supportCase.Id,
            vendorResponsibilityAmount,
            $"Vendor recovery staged for support case {supportCase.Id}.");

        if (existingRecovery is null)
        {
            _context.VendorRecoveries.Add(recovery);
        }

        var remaining = recovery.OutstandingAmount;
        if (remaining <= 0.009m)
        {
            return recovery;
        }

        var settlementItem = await _context.SettlementItems
            .Include(item => item.Settlement)
                .ThenInclude(settlement => settlement.Payouts)
            .Where(item => item.OrderId == supportCase.OrderId && item.Settlement.VendorId == supportCase.Order.VendorId)
            .OrderByDescending(item => item.Settlement.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var settlement = settlementItem?.Settlement;
        var payout = settlement?.Payouts
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        if (settlementItem is not null &&
            settlement is not null &&
            settlement.Status != SettlementStatus.Settled &&
            payout?.Status != PayoutStatus.Paid)
        {
            var holdRecoveryAmount = Math.Min(
                remaining,
                Math.Min(settlementItem.VendorAmount, payout?.Amount ?? settlementItem.VendorAmount));

            if (holdRecoveryAmount > 0m)
            {
                settlementItem.ApplyVendorRecovery(holdRecoveryAmount);
                settlement.ApplyVendorRecovery(holdRecoveryAmount);
                payout?.ReduceAmount(holdRecoveryAmount);

                var walletTransactionId = await _vendorPayoutWalletService.RecoverFromHoldAsync(
                    supportCase.Order.VendorId,
                    settlement.Id,
                    recovery.Id,
                    holdRecoveryAmount,
                    $"Vendor recovery captured from held payout for order {supportCase.Order.OrderNumber}.",
                    cancellationToken);

                recovery.ApplyRecovery(
                    holdRecoveryAmount,
                    VendorRecoverySource.UnsettledPayoutHold,
                    settlement.Id,
                    payout?.Id,
                    walletTransactionId);

                remaining = recovery.OutstandingAmount;
            }
        }

        if (remaining > 0.009m)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(
                    item => item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == supportCase.Order.VendorId,
                    cancellationToken);

            var walletRecoveryAmount = Math.Min(remaining, wallet?.CurrentBalance ?? 0m);
            if (wallet is not null && walletRecoveryAmount > 0m)
            {
                wallet.Debit(walletRecoveryAmount);
                var walletTxn = new WalletTransaction(
                    wallet.Id,
                    WalletTxnType.Debit,
                    walletRecoveryAmount,
                    "OUT",
                    orderId: supportCase.OrderId,
                    referenceType: "VendorRecovery",
                    referenceId: recovery.Id,
                    description: $"Vendor recovery debited from wallet for order {supportCase.Order.OrderNumber}.");
                _context.WalletTransactions.Add(walletTxn);

                recovery.ApplyRecovery(
                    walletRecoveryAmount,
                    VendorRecoverySource.VendorWalletDebit,
                    walletTransactionId: walletTxn.Id);
            }
        }

        if (recovery.HasOutstandingBalance)
        {
            recovery.KeepPending("Remaining vendor recovery will be deducted from future settlements.");
        }

        return recovery;
    }

    public async Task<decimal> ApplyOutstandingRecoveriesAsync(
        Guid vendorId,
        Guid orderId,
        decimal vendorNet,
        CancellationToken cancellationToken = default)
    {
        if (vendorNet <= 0m)
        {
            return 0m;
        }

        var outstandingRecoveries = await _context.VendorRecoveries
            .Where(item =>
                item.VendorId == vendorId &&
                item.OrderId != orderId &&
                item.OutstandingAmount > 0m)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (outstandingRecoveries.Count == 0)
        {
            return 0m;
        }

        var recoveredAmount = 0m;
        var remainingVendorNet = vendorNet;

        foreach (var recovery in outstandingRecoveries)
        {
            if (remainingVendorNet <= 0.009m)
            {
                break;
            }

            var deduction = Math.Min(remainingVendorNet, recovery.OutstandingAmount);
            if (deduction <= 0m)
            {
                continue;
            }

            recovery.ApplyRecovery(deduction, VendorRecoverySource.FutureSettlementDeduction);
            recoveredAmount += deduction;
            remainingVendorNet -= deduction;
        }

        return recoveredAmount;
    }

    private static decimal ResolveVendorResponsibilityAmount(decimal approvedAmount, string? costBearer)
    {
        var normalizedCostBearer = string.IsNullOrWhiteSpace(costBearer)
            ? "platform"
            : costBearer.Trim().ToLowerInvariant();

        return normalizedCostBearer switch
        {
            "vendor" => approvedAmount,
            "shared" => Math.Round(approvedAmount / 2m, 2, MidpointRounding.AwayFromZero),
            _ => 0m
        };
    }
}
