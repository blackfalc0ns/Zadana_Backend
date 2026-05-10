namespace Zadana.Domain.Modules.Wallets.Enums;

public enum SettlementStatus
{
    Pending,
    PendingReview,
    Approved,
    OnHold,
    Processing,
    Settled,
    PaidOut,
    PayoutFailed,
    Failed,
    Reversed,
    Rejected,
    Disputed
}
