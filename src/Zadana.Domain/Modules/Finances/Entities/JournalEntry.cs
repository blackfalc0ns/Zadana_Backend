using Zadana.Domain.Modules.Finances.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

public class JournalEntry : BaseEntity
{
    private readonly List<JournalLine> _lines = [];

    public Guid FinancialEventId { get; private set; }
    public long SequenceNumber { get; private set; }
    public JournalEntryStatus Status { get; private set; }
    public string CurrencyCode { get; private set; } = "SAR";
    public DateTime PostedAtUtc { get; private set; }
    public string? Memo { get; private set; }

    public FinancialEvent FinancialEvent { get; private set; } = null!;
    public IReadOnlyCollection<JournalLine> Lines => _lines.AsReadOnly();

    private JournalEntry() { }

    public JournalEntry(
        Guid financialEventId,
        long sequenceNumber,
        string currencyCode = "SAR",
        DateTime? postedAtUtc = null,
        string? memo = null)
    {
        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number must be greater than zero.");
        }

        FinancialEventId = financialEventId;
        SequenceNumber = sequenceNumber;
        CurrencyCode = NormalizeCurrency(currencyCode);
        PostedAtUtc = postedAtUtc ?? DateTime.UtcNow;
        Status = JournalEntryStatus.Posted;
        Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
    }

    public void AddLine(JournalLine line)
    {
        if (line.CurrencyCode != CurrencyCode)
        {
            throw new InvalidOperationException("Journal line currency must match entry currency.");
        }

        _lines.Add(line);
    }

    public void EnsureBalanced()
    {
        if (_lines.Count < 2)
        {
            throw new InvalidOperationException("A journal entry must contain at least two lines.");
        }

        var debitTotal = _lines.Sum(line => line.DebitAmount);
        var creditTotal = _lines.Sum(line => line.CreditAmount);

        if (debitTotal != creditTotal)
        {
            throw new InvalidOperationException("Journal entry must balance debits and credits.");
        }
    }

    private static string NormalizeCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return "SAR";
        }

        return currencyCode.Trim().ToUpperInvariant();
    }
}
