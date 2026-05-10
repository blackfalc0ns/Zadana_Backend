using Zadana.Domain.Modules.Finances.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

public class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; private set; }
    public FinancialAccountCode AccountCode { get; private set; }
    public FinancialOwnerType? OwnerType { get; private set; }
    public Guid? OwnerId { get; private set; }
    public decimal DebitAmount { get; private set; }
    public decimal CreditAmount { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public Guid? OrderId { get; private set; }
    public Guid? SettlementId { get; private set; }
    public Guid? PayoutId { get; private set; }
    public string? Memo { get; private set; }

    public JournalEntry JournalEntry { get; private set; } = null!;

    private JournalLine() { }

    public JournalLine(
        Guid journalEntryId,
        FinancialAccountCode accountCode,
        decimal debitAmount,
        decimal creditAmount,
        string currencyCode = "EGP",
        FinancialOwnerType? ownerType = null,
        Guid? ownerId = null,
        Guid? orderId = null,
        Guid? settlementId = null,
        Guid? payoutId = null,
        string? memo = null)
    {
        if (debitAmount < 0 || creditAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(debitAmount), "Journal amounts cannot be negative.");
        }

        if ((debitAmount == 0 && creditAmount == 0) || (debitAmount > 0 && creditAmount > 0))
        {
            throw new InvalidOperationException("A journal line must be either debit or credit.");
        }

        JournalEntryId = journalEntryId;
        AccountCode = accountCode;
        DebitAmount = debitAmount;
        CreditAmount = creditAmount;
        CurrencyCode = NormalizeCurrency(currencyCode);
        OwnerType = ownerType;
        OwnerId = ownerId;
        OrderId = orderId;
        SettlementId = settlementId;
        PayoutId = payoutId;
        Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
    }

    private static string NormalizeCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return "EGP";
        }

        return currencyCode.Trim().ToUpperInvariant();
    }
}
