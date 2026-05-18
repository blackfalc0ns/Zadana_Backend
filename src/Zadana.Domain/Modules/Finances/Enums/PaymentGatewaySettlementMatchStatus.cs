namespace Zadana.Domain.Modules.Finances.Enums;

/// <summary>
/// Per-line match outcome inside a payment-gateway settlement file.
/// </summary>
public enum PaymentGatewaySettlementMatchStatus
{
    Matched = 0,
    MissingOrder = 1,
    AmountMismatch = 2,
    Duplicate = 3,
    Ignored = 4,
}
