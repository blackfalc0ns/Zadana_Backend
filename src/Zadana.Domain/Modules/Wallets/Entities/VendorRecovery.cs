using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class VendorRecovery : BaseEntity
{
    public Guid VendorId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderSupportCaseId { get; private set; }
    public decimal TargetAmount { get; private set; }
    public decimal RecoveredAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public VendorRecoveryStatus Status { get; private set; }
    public VendorRecoverySource? Source { get; private set; }
    public Guid? SettlementId { get; private set; }
    public Guid? PayoutId { get; private set; }
    public Guid? WalletTransactionId { get; private set; }
    public string? Notes { get; private set; }

    private VendorRecovery()
    {
    }

    public VendorRecovery(
        Guid vendorId,
        Guid orderId,
        Guid orderSupportCaseId,
        decimal targetAmount,
        string? notes = null)
    {
        if (targetAmount <= 0)
        {
            throw new BusinessRuleException("INVALID_VENDOR_RECOVERY_AMOUNT", "Vendor recovery amount must be greater than zero.");
        }

        VendorId = vendorId;
        OrderId = orderId;
        OrderSupportCaseId = orderSupportCaseId;
        TargetAmount = targetAmount;
        RecoveredAmount = 0m;
        OutstandingAmount = targetAmount;
        Status = VendorRecoveryStatus.Pending;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public bool HasOutstandingBalance => OutstandingAmount > 0.009m;

    public void ApplyRecovery(
        decimal amount,
        VendorRecoverySource source,
        Guid? settlementId = null,
        Guid? payoutId = null,
        Guid? walletTransactionId = null)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_VENDOR_RECOVERY_AMOUNT", "Vendor recovery amount must be greater than zero.");
        }

        if (amount > OutstandingAmount)
        {
            throw new BusinessRuleException("VENDOR_RECOVERY_EXCEEDS_OUTSTANDING", "Recovery amount exceeds the outstanding balance.");
        }

        RecoveredAmount += amount;
        OutstandingAmount = Math.Max(0m, TargetAmount - RecoveredAmount);
        SettlementId ??= settlementId;
        PayoutId ??= payoutId;
        WalletTransactionId ??= walletTransactionId;
        Source = Source is null || Source == source ? source : VendorRecoverySource.Mixed;
        Status = OutstandingAmount <= 0.009m ? VendorRecoveryStatus.Recovered : VendorRecoveryStatus.PartiallyRecovered;
    }

    public void KeepPending(string? notes = null)
    {
        Status = RecoveredAmount > 0m ? VendorRecoveryStatus.PartiallyRecovered : VendorRecoveryStatus.Pending;
        Notes = string.IsNullOrWhiteSpace(notes) ? Notes : notes.Trim();
    }
}
