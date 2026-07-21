using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class Payout : BaseEntity
{
    public Guid SettlementId { get; private set; }
    public Guid? VendorBankAccountId { get; private set; }
    public PayoutDestinationType DestinationType { get; private set; }
    public string? DestinationSnapshot { get; private set; }
    /// <summary>
    /// The beneficiary's chosen day captured when this payout is prepared.
    /// Later preference changes only affect new payouts, never one that finance
    /// has already reviewed or claimed.
    /// </summary>
    public PayoutScheduleDay? ScheduledPayoutDay { get; private set; }
    public decimal Amount { get; private set; }
    public PayoutStatus Status { get; private set; }
    public string ProviderName { get; private set; } = "Manual";
    public string? ProviderTransferId { get; private set; }
    public string? ProviderSequenceNumber { get; private set; }
    public string? TransferReference { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? TriggeredAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    // Navigation
    public Settlement Settlement { get; private set; } = null!;
    public VendorBankAccount? VendorBankAccount { get; private set; }
    public ICollection<PayoutAttempt> Attempts { get; private set; } = [];
    public PayoutManualConfirmation? ManualConfirmation { get; private set; }
    public PayoutExecutionReservation? ExecutionReservation { get; private set; }
    public PayoutReversal? Reversal { get; private set; }
    public ICollection<PayoutProofAttachment> ProofAttachments { get; private set; } = [];

    private Payout() { }

    public Payout(Guid settlementId, decimal amount, Guid? vendorBankAccountId = null)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Payout amount must be greater than zero.");

        SettlementId = settlementId;
        Amount = amount;
        VendorBankAccountId = vendorBankAccountId;
        DestinationType = vendorBankAccountId.HasValue ? PayoutDestinationType.VendorBankAccount : PayoutDestinationType.Manual;
        ProviderName = "Manual";
        Status = PayoutStatus.Pending;
    }

    public void PrepareDestination(PayoutDestinationType destinationType, string? destinationSnapshot)
    {
        DestinationType = destinationType;
        DestinationSnapshot = string.IsNullOrWhiteSpace(destinationSnapshot) ? null : destinationSnapshot.Trim();
    }

    public void SetScheduledPayoutDay(PayoutScheduleDay payoutDay)
    {
        if (Status is PayoutStatus.Paid or PayoutStatus.Reversed or PayoutStatus.Cancelled)
        {
            throw new BusinessRuleException(
                "PAYOUT_ALREADY_CLOSED",
                "The scheduled payout day cannot be changed for a closed payout.");
        }

        ScheduledPayoutDay = PayoutScheduleDayPolicy.EnsureAllowed(payoutDay);
    }

    public void AssignProcessedBy(Guid processedByUserId)
    {
        if (processedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "PAYOUT_PROCESSING_USER_REQUIRED",
                "An authenticated finance administrator is required to process a payout.");
        }

        ProcessedByUserId ??= processedByUserId;
    }

    public void MarkQueued(
        string? providerTransferId = null,
        string? providerName = null,
        string? providerSequenceNumber = null)
    {
        Status = PayoutStatus.Queued;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? ProviderName : providerName.Trim();
        ProviderTransferId = string.IsNullOrWhiteSpace(providerTransferId) ? ProviderTransferId : providerTransferId.Trim();
        ProviderSequenceNumber = string.IsNullOrWhiteSpace(providerSequenceNumber) ? ProviderSequenceNumber : providerSequenceNumber.Trim();
        TriggeredAtUtc = DateTime.UtcNow;
        CompletedAtUtc = null;
        FailureReason = null;
    }

    public void MarkAsProcessing(
        string? providerTransferId = null,
        string? providerName = null,
        string? providerSequenceNumber = null)
    {
        Status = PayoutStatus.Processing;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? ProviderName : providerName.Trim();
        ProviderTransferId = string.IsNullOrWhiteSpace(providerTransferId) ? ProviderTransferId : providerTransferId.Trim();
        ProviderSequenceNumber = string.IsNullOrWhiteSpace(providerSequenceNumber) ? ProviderSequenceNumber : providerSequenceNumber.Trim();
        TriggeredAtUtc ??= DateTime.UtcNow;
        CompletedAtUtc = null;
        FailureReason = null;
    }

    public void MarkAsPaid(
        string transferReference,
        string? providerTransferId = null,
        string? providerName = null,
        string? providerSequenceNumber = null)
    {
        if (Status == PayoutStatus.Reversed)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_REVERSED", "A reversed payout cannot be marked as paid again.");
        }

        Status = PayoutStatus.Paid;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? ProviderName : providerName.Trim();
        ProviderTransferId = string.IsNullOrWhiteSpace(providerTransferId) ? ProviderTransferId : providerTransferId.Trim();
        ProviderSequenceNumber = string.IsNullOrWhiteSpace(providerSequenceNumber) ? ProviderSequenceNumber : providerSequenceNumber.Trim();
        TransferReference = transferReference.Trim();
        CompletedAtUtc = DateTime.UtcNow;
        ProcessedAtUtc = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkAsManuallyPaid(string transferReference, Guid processedByUserId)
    {
        if (Status == PayoutStatus.Reversed)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_REVERSED", "A reversed payout cannot be marked as paid again.");
        }

        if (string.IsNullOrWhiteSpace(transferReference))
        {
            throw new BusinessRuleException("TRANSFER_REFERENCE_REQUIRED", "Transfer reference is required for manual payout completion.");
        }

        if (processedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "The confirming administrator is required.");
        }

        Status = PayoutStatus.Paid;
        ProviderName = "Manual";
        ProviderTransferId = null;
        ProviderSequenceNumber = null;
        TransferReference = transferReference.Trim();
        ProcessedByUserId = processedByUserId;
        CompletedAtUtc = DateTime.UtcNow;
        ProcessedAtUtc = DateTime.UtcNow;
        FailureReason = null;
    }

    public void ReduceAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (Status == PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_PAID", "Paid payouts cannot be reduced.");
        }

        if (Amount < amount)
        {
            throw new BusinessRuleException("PAYOUT_RECOVERY_EXCEEDS_AMOUNT", "Recovery amount exceeds payout amount.");
        }

        Amount -= amount;
    }

    public void MarkAsFailed(
        string? failureReason = null,
        string? providerTransferId = null,
        string? providerName = null,
        string? providerSequenceNumber = null)
    {
        if (Status is PayoutStatus.Paid or PayoutStatus.Reversed)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "A closed payout cannot be marked as failed.");
        }

        Status = PayoutStatus.Failed;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? ProviderName : providerName.Trim();
        ProviderTransferId = string.IsNullOrWhiteSpace(providerTransferId) ? ProviderTransferId : providerTransferId.Trim();
        ProviderSequenceNumber = string.IsNullOrWhiteSpace(providerSequenceNumber) ? ProviderSequenceNumber : providerSequenceNumber.Trim();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is PayoutStatus.Paid or PayoutStatus.Reversed)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "A closed payout cannot be cancelled.");
        }

        if (Status is PayoutStatus.Queued or PayoutStatus.Processing)
        {
            throw new BusinessRuleException(
                "PAYOUT_IN_FLIGHT_CANNOT_CANCEL",
                "An in-flight payout must be reconciled with its execution channel instead of cancelled.");
        }

        Status = PayoutStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsReversed()
    {
        if (Status != PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_REVERSAL_INVALID_STATUS", "Only a paid payout can be reversed after funds are returned.");
        }

        Status = PayoutStatus.Reversed;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
