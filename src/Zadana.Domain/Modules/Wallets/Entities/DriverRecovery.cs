using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class DriverRecovery : BaseEntity
{
    public Guid DriverId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderSupportCaseId { get; private set; }
    public decimal TargetAmount { get; private set; }
    public decimal RecoveredAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public DriverRecoveryStatus Status { get; private set; }
    public DriverRecoverySource? Source { get; private set; }
    public Guid? WalletTransactionId { get; private set; }
    public Guid? PayoutId { get; private set; }
    public string? Notes { get; private set; }

    private DriverRecovery()
    {
    }

    public DriverRecovery(
        Guid driverId,
        Guid orderId,
        Guid orderSupportCaseId,
        decimal targetAmount,
        string? notes = null)
    {
        if (targetAmount <= 0)
        {
            throw new BusinessRuleException("INVALID_DRIVER_RECOVERY_AMOUNT", "Driver recovery amount must be greater than zero.");
        }

        DriverId = driverId;
        OrderId = orderId;
        OrderSupportCaseId = orderSupportCaseId;
        TargetAmount = targetAmount;
        RecoveredAmount = 0m;
        OutstandingAmount = targetAmount;
        Status = DriverRecoveryStatus.Pending;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public bool HasOutstandingBalance => OutstandingAmount > 0.009m;

    public void ApplyRecovery(
        decimal amount,
        DriverRecoverySource source,
        Guid? walletTransactionId = null,
        Guid? payoutId = null)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_DRIVER_RECOVERY_AMOUNT", "Driver recovery amount must be greater than zero.");
        }

        if (amount > OutstandingAmount)
        {
            throw new BusinessRuleException("DRIVER_RECOVERY_EXCEEDS_OUTSTANDING", "Recovery amount exceeds the outstanding balance.");
        }

        RecoveredAmount += amount;
        OutstandingAmount = Math.Max(0m, TargetAmount - RecoveredAmount);
        WalletTransactionId ??= walletTransactionId;
        PayoutId ??= payoutId;
        Source = Source is null || Source == source ? source : DriverRecoverySource.Mixed;
        Status = OutstandingAmount <= 0.009m ? DriverRecoveryStatus.Recovered : DriverRecoveryStatus.PartiallyRecovered;
    }

    public void KeepPending(string? notes = null)
    {
        Status = RecoveredAmount > 0m ? DriverRecoveryStatus.PartiallyRecovered : DriverRecoveryStatus.Pending;
        Notes = string.IsNullOrWhiteSpace(notes) ? Notes : notes.Trim();
    }
}
