namespace Zadana.Domain.Modules.Payments.Enums;

/// <summary>
/// Who absorbs the cost of a refund.
/// </summary>
public enum RefundCostBearerKind
{
    Vendor = 0,
    Driver = 1,
    Platform = 2,
    Shared = 3,
}
