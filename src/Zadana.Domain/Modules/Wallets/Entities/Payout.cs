using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class Payout : BaseEntity
{
    public Guid SettlementId { get; private set; }
    public Guid? VendorBankAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public PayoutStatus Status { get; private set; }
    public string? TransferReference { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    // Navigation
    public Settlement Settlement { get; private set; } = null!;
    public VendorBankAccount? VendorBankAccount { get; private set; }

    private Payout() { }

    public Payout(Guid settlementId, decimal amount, Guid? vendorBankAccountId = null)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Payout amount must be greater than zero.");

        SettlementId = settlementId;
        Amount = amount;
        VendorBankAccountId = vendorBankAccountId;
        Status = PayoutStatus.Pending;
    }

    public void MarkAsProcessing() => Status = PayoutStatus.Processing;

    public void MarkAsPaid(string transferReference)
    {
        Status = PayoutStatus.Paid;
        TransferReference = transferReference.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsFailed() => Status = PayoutStatus.Failed;
    public void Cancel() => Status = PayoutStatus.Cancelled;
}
