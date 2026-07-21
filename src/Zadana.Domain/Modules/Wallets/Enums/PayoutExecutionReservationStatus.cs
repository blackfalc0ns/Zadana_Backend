namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Lifecycle of the durable execution reservation for a payout. Submitted
/// means the external bank/gateway operation may already exist and therefore
/// must be reconciled instead of retried or cancelled.
/// </summary>
public enum PayoutExecutionReservationStatus
{
    Claimed,
    Submitted,
    Confirmed,
    Released
}
