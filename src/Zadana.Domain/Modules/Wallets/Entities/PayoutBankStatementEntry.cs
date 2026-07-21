using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// A minimally retained outbound payment row. Beneficiary information is kept
/// masked so a reconciliation export does not become a second source of full
/// bank-account PII.
/// </summary>
public sealed class PayoutBankStatementEntry : BaseEntity
{
    public Guid ImportId { get; private set; }
    public int RowNumber { get; private set; }
    public string BankReference { get; private set; } = null!;
    public string NormalizedBankReference { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateTime TransactionDateUtc { get; private set; }
    public string CurrencyCode { get; private set; } = "SAR";
    public string? BeneficiaryMasked { get; private set; }
    public string? Memo { get; private set; }
    public PayoutBankStatementEntryStatus Status { get; private set; }
    public Guid? PayoutId { get; private set; }
    public Guid? MatchedByUserId { get; private set; }
    public DateTime? MatchedAtUtc { get; private set; }
    public string? ResolutionNote { get; private set; }

    public PayoutBankStatementImport Import { get; private set; } = null!;
    public Payout? Payout { get; private set; }

    private PayoutBankStatementEntry() { }

    public PayoutBankStatementEntry(
        Guid importId,
        int rowNumber,
        string bankReference,
        string normalizedBankReference,
        decimal amount,
        DateTime transactionDateUtc,
        string? beneficiaryMasked = null,
        string? memo = null,
        string currencyCode = "SAR")
    {
        if (importId == Guid.Empty)
        {
            throw new BusinessRuleException("BANK_STATEMENT_IMPORT_REQUIRED", "Bank statement import is required.");
        }

        if (rowNumber <= 0)
        {
            throw new BusinessRuleException("BANK_STATEMENT_ROW_INVALID", "Bank statement row number must be positive.");
        }

        if (string.IsNullOrWhiteSpace(bankReference) || string.IsNullOrWhiteSpace(normalizedBankReference))
        {
            throw new BusinessRuleException("BANK_STATEMENT_REFERENCE_REQUIRED", "Bank statement reference is required.");
        }

        if (amount <= 0m)
        {
            throw new BusinessRuleException("INVALID_AMOUNT", "Bank statement amount must be greater than zero.");
        }

        ImportId = importId;
        RowNumber = rowNumber;
        BankReference = bankReference.Trim();
        NormalizedBankReference = normalizedBankReference.Trim().ToUpperInvariant();
        Amount = amount;
        TransactionDateUtc = DateTime.SpecifyKind(transactionDateUtc.Date, DateTimeKind.Utc);
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "SAR" : currencyCode.Trim().ToUpperInvariant();
        BeneficiaryMasked = NormalizeOptional(beneficiaryMasked, 256);
        Memo = NormalizeOptional(memo, 500);
        Status = PayoutBankStatementEntryStatus.Unmatched;
    }

    public void MarkAmbiguous(string? note = null) =>
        SetUnmatchedStatus(PayoutBankStatementEntryStatus.Ambiguous, note);

    public void MarkMismatch(string? note = null) =>
        SetUnmatchedStatus(PayoutBankStatementEntryStatus.Mismatch, note);

    public void MarkIgnored(Guid resolvedByUserId, string? note = null)
    {
        EnsureResolver(resolvedByUserId);
        Status = PayoutBankStatementEntryStatus.Ignored;
        PayoutId = null;
        MatchedByUserId = resolvedByUserId;
        MatchedAtUtc = DateTime.UtcNow;
        ResolutionNote = NormalizeOptional(note, 1000);
    }

    public void Match(Guid payoutId, Guid resolvedByUserId, string? note = null)
    {
        if (payoutId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_REQUIRED", "Payout is required for a bank statement match.");
        }

        EnsureResolver(resolvedByUserId);
        Status = PayoutBankStatementEntryStatus.Matched;
        PayoutId = payoutId;
        MatchedByUserId = resolvedByUserId;
        MatchedAtUtc = DateTime.UtcNow;
        ResolutionNote = NormalizeOptional(note, 1000);
    }

    private void SetUnmatchedStatus(PayoutBankStatementEntryStatus status, string? note)
    {
        Status = status;
        PayoutId = null;
        MatchedByUserId = null;
        MatchedAtUtc = null;
        ResolutionNote = NormalizeOptional(note, 1000);
    }

    private static void EnsureResolver(Guid resolvedByUserId)
    {
        if (resolvedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "An authenticated finance administrator is required.");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
}
