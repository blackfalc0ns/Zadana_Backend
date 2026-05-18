namespace Zadana.Domain.Modules.Payments.Enums;

/// <summary>
/// Operational state of a refund request, distinct from the payment status.
/// Used by the refund posting pipeline (section 12 of the spec).
/// </summary>
public enum RefundStatus
{
    Requested = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}
