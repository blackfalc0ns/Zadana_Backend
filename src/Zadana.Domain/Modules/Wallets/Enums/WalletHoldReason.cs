namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Why a portion of a wallet's balance is held and not available for withdrawal.
/// </summary>
public enum WalletHoldReason
{
    Settlement = 0,
    Withdrawal = 1,
    Payout = 2,
    ManualReview = 3,
    Dispute = 4,
}
