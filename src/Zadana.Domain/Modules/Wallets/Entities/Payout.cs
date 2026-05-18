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
    public decimal Amount { get; private set; }
    public PayoutStatus Status { get; private set; }
    public string ProviderName { get; private set; } = "Manual";
    public string? ProviderTransferId { get; private set; }
    public string? TransferReference { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? TriggeredAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    // Navigation
    public Settlement Settlement { get; private set; } = null!;
    public VendorBankAccount? VendorBankAccount { get; private set; }
    public ICollection<PayoutAttempt> Attempts { get; private set; } = [];

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

    public void MarkQueued(string? providerTransferId = null)
    {
        Status = PayoutStatus.Queued;
        ProviderTransferId = string.IsNullOrWhiteSpace(providerTransferId) ? ProviderTransferId : providerTransferId.Trim();
        TriggeredAtUtc = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkAsProcessing() => Status = PayoutStatus.Processing;

    public void MarkAsPaid(string transferReference)
    {
        Status = PayoutStatus.Paid;
        TransferReference = transferReference.Trim();
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

    public void MarkAsFailed(string? failureReason = null)
    {
        Status = PayoutStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = PayoutStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
