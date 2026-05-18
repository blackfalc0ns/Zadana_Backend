namespace Zadana.Domain.Modules.Payments.Enums;

/// <summary>
/// How a customer is reimbursed when a refund is approved.
/// </summary>
public enum RefundCompensationMethod
{
    SameMethod = 0,
    Coupon = 1,
    Manual = 2,
}
