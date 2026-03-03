using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class Wallet : BaseEntity
{
    public WalletOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public decimal PendingBalance { get; private set; }

    public ICollection<WalletTransaction> Transactions { get; private set; } = [];

    private Wallet() { }

    public Wallet(WalletOwnerType ownerType, Guid ownerId)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        CurrentBalance = 0;
        PendingBalance = 0;
    }

    public void Credit(decimal amount)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Credit amount must be greater than zero.");
        CurrentBalance += amount;
    }

    public void Debit(decimal amount)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Debit amount must be greater than zero.");
        if (CurrentBalance < amount) throw new BusinessRuleException("INSUFFICIENT_FUNDS", "Insufficient wallet balance.");
        CurrentBalance -= amount;
    }

    public void Hold(decimal amount)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Hold amount must be greater than zero.");
        if (CurrentBalance < amount) throw new BusinessRuleException("INSUFFICIENT_FUNDS", "Insufficient wallet balance to hold.");
        
        CurrentBalance -= amount;
        PendingBalance += amount;
    }

    public void ReleaseHold(decimal amount)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Release amount must be greater than zero.");
        if (PendingBalance < amount) throw new BusinessRuleException("INVALID_HOLD_RELEASE", "Release amount exceeds pending balance.");

        PendingBalance -= amount;
        CurrentBalance += amount;
    }

    public void SettleHold(decimal amount)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Settle amount must be greater than zero.");
        if (PendingBalance < amount) throw new BusinessRuleException("INVALID_HOLD_SETTLE", "Settle amount exceeds pending balance.");

        PendingBalance -= amount;
    }
}
