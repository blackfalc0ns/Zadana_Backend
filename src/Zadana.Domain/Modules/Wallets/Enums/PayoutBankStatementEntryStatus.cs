namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Reconciliation result for an individual outbound-bank-statement row.
/// Matching a row does not itself mark a payout as paid; finance still has to
/// complete the evidence and approval workflow.
/// </summary>
public enum PayoutBankStatementEntryStatus
{
    Unmatched,
    Matched,
    Ambiguous,
    Mismatch,
    Ignored
}
