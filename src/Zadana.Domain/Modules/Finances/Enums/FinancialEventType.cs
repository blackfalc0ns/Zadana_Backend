namespace Zadana.Domain.Modules.Finances.Enums;

/// <summary>
/// Discriminator for ledger postings. Existing values are preserved to keep
/// historical data intact; new values follow the revised SAR-only workflow
/// (sections 8, 10, 11, 12, 13, 16 of the spec).
/// </summary>
public enum FinancialEventType
{
    // Legacy values - kept for backward compatibility with persisted data.
    OnlinePaymentDelivered = 0,
    CodCashCollected = 1,
    VendorPayoutPaid = 2,
    DriverPayoutPaid = 3,
    DriverCashRemittance = 4,
    RefundIssued = 5,
    RecoveryCreated = 6,
    FinancialAdjustmentApplied = 7,
    PayoutSucceeded = 8,
    PayoutFailed = 9,

    // Online payments (Moyasar).
    OnlinePaymentCaptured = 100,
    OnlineOrderDelivered = 101,

    // Bank transfers and cash.
    BankTransferConfirmed = 110,

    // Gateway settlement (provider payouts).
    GatewaySettlementReceived = 120,

    // Refunds.
    RefundCompleted = 130,

    // Wallet hold lifecycle (informational - do not produce balance-changing journal lines on their own).
    WalletHoldCreated = 200,
    WalletHoldReleased = 201,
    WalletHoldConsumed = 202,

    // Returned bank payouts. These post the exact opposite of the original
    // payout journal and keep the original payout entry immutable.
    VendorPayoutReversed = 210,
    DriverPayoutReversed = 211,
}
