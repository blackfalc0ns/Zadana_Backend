namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Controls how new settlement payouts are completed. This is intentionally
/// independent from payment-gateway configuration, so historical gateway
/// payouts can still be reconciled while new payouts are handled manually.
/// </summary>
public enum SettlementProcessingMode
{
    Automatic,
    Manual
}
