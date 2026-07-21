using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Immutable audit header for a bank-statement CSV imported by finance.
/// The original file is intentionally not stored as a public media URL; its
/// SHA-256 digest makes a repeated import detectable without exposing a bank
/// statement outside the authorised reconciliation API.
/// </summary>
public sealed class PayoutBankStatementImport : BaseEntity
{
    public string FileName { get; private set; } = null!;
    public string FileSha256 { get; private set; } = null!;
    public Guid ImportedByUserId { get; private set; }
    public DateTime ImportedAtUtc { get; private set; }
    public int TotalRows { get; private set; }
    public int MatchedRows { get; private set; }
    public int UnmatchedRows { get; private set; }
    public int AmbiguousRows { get; private set; }
    public int MismatchRows { get; private set; }
    public int InvalidRows { get; private set; }

    public ICollection<PayoutBankStatementEntry> Entries { get; private set; } = [];

    private PayoutBankStatementImport() { }

    public PayoutBankStatementImport(string fileName, string fileSha256, Guid importedByUserId)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BusinessRuleException("BANK_STATEMENT_FILE_NAME_REQUIRED", "Bank statement file name is required.");
        }

        if (string.IsNullOrWhiteSpace(fileSha256) || fileSha256.Trim().Length != 64)
        {
            throw new BusinessRuleException("BANK_STATEMENT_HASH_INVALID", "A valid SHA-256 digest is required for the bank statement.");
        }

        if (importedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "An authenticated finance administrator is required.");
        }

        FileName = Path.GetFileName(fileName.Trim());
        FileSha256 = fileSha256.Trim().ToUpperInvariant();
        ImportedByUserId = importedByUserId;
        ImportedAtUtc = DateTime.UtcNow;
    }

    public void SetSummary(
        int totalRows,
        int matchedRows,
        int unmatchedRows,
        int ambiguousRows,
        int mismatchRows,
        int invalidRows)
    {
        TotalRows = Math.Max(0, totalRows);
        MatchedRows = Math.Max(0, matchedRows);
        UnmatchedRows = Math.Max(0, unmatchedRows);
        AmbiguousRows = Math.Max(0, ambiguousRows);
        MismatchRows = Math.Max(0, mismatchRows);
        InvalidRows = Math.Max(0, invalidRows);
    }
}
