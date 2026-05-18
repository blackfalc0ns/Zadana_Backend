namespace Zadana.Domain.Modules.Finances.Enums;

/// <summary>
/// Lifecycle of an imported payment-gateway settlement file/report.
/// </summary>
public enum PaymentGatewaySettlementStatus
{
    Imported = 0,
    Reconciled = 1,
    Variance = 2,
    Posted = 3,
}
