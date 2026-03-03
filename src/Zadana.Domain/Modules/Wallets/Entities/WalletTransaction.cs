using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class WalletTransaction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WalletId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? SettlementId { get; private set; }
    
    public WalletTxnType TxnType { get; private set; }
    public decimal Amount { get; private set; }
    public string Direction { get; private set; } = null!; // "IN" or "OUT"
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation
    public Wallet Wallet { get; private set; } = null!;
    public Order? Order { get; private set; }
    public Payment? Payment { get; private set; }
    public Settlement? Settlement { get; private set; }

    private WalletTransaction() { }

    public WalletTransaction(
        Guid walletId,
        WalletTxnType txnType,
        decimal amount,
        string direction,
        Guid? orderId = null,
        Guid? paymentId = null,
        Guid? settlementId = null,
        string? referenceType = null,
        Guid? referenceId = null,
        string? description = null)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Transaction amount must be greater than zero.");
        if (direction != "IN" && direction != "OUT") throw new BusinessRuleException("INVALID_DIRECTION", "Direction must be 'IN' or 'OUT'.");

        WalletId = walletId;
        TxnType = txnType;
        Amount = amount;
        Direction = direction;
        OrderId = orderId;
        PaymentId = paymentId;
        SettlementId = settlementId;
        ReferenceType = referenceType?.Trim();
        ReferenceId = referenceId;
        Description = description?.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
