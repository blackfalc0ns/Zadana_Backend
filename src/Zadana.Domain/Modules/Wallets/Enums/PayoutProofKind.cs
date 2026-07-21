namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Identifies the finance event a protected proof file may be used for.
/// A proof is deliberately scoped to one payout and one kind so it cannot be
/// replayed as evidence for a different transfer or a return of funds.
/// </summary>
public enum PayoutProofKind
{
    ManualTransfer,
    ReturnedFunds
}
