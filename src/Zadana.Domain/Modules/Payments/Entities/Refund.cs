using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Payments.Entities;

public class Refund : BaseEntity
{
    public Guid PaymentId { get; private set; }
    public Guid? OrderSupportCaseId { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reason { get; private set; }
    public string? RefundMethod { get; private set; }
    public string? CostBearer { get; private set; }
    public PaymentStatus Status { get; private set; }

    /// <summary>Provider that issued the refund (e.g. "Moyasar"). Null for manual/coupon refunds.</summary>
    public string? ProviderName { get; private set; }

    /// <summary>Provider-side refund identifier returned after the refund call succeeds.</summary>
    public string? ProviderRefundId { get; private set; }

    /// <summary>Originally requested amount (before approval).</summary>
    public decimal RequestedAmount { get; private set; }

    /// <summary>Amount actually approved/refunded. Equals <see cref="Amount"/> for the simple case.</summary>
    public decimal ApprovedAmount { get; private set; }

    /// <summary>Currency of the refund. SAR for new refunds.</summary>
    public string Currency { get; private set; } = "SAR";

    /// <summary>Operational lifecycle of the refund (Requested/Processing/Succeeded/Failed/Cancelled).</summary>
    public RefundStatus LifecycleStatus { get; private set; }

    /// <summary>How the customer was reimbursed.</summary>
    public RefundCompensationMethod CompensationMethod { get; private set; }

    /// <summary>Raw provider response of the most recent refund call.</summary>
    public string? RawProviderResponse { get; private set; }

    public DateTime? SucceededAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }

    // Navigation
    public Payment Payment { get; private set; } = null!;

    private Refund() { }

    public Refund(Guid paymentId, decimal amount, string? reason = null, string? refundMethod = null, string? costBearer = null, Guid? orderSupportCaseId = null)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Refund amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(costBearer)) throw new BusinessRuleException("REFUND_COST_BEARER_REQUIRED", "Refund cost bearer is required.");

        PaymentId = paymentId;
        OrderSupportCaseId = orderSupportCaseId;
        Amount = amount;
        Reason = reason?.Trim();
        RefundMethod = string.IsNullOrWhiteSpace(refundMethod) ? null : refundMethod.Trim();
        CostBearer = costBearer.Trim();
        Status = PaymentStatus.Initiated;
        RequestedAmount = amount;
        ApprovedAmount = amount;
        Currency = "SAR";
        LifecycleStatus = RefundStatus.Requested;
        CompensationMethod = ResolveCompensationMethod(refundMethod);
    }

    private static RefundCompensationMethod ResolveCompensationMethod(string? refundMethod)
    {
        if (string.IsNullOrWhiteSpace(refundMethod)) return RefundCompensationMethod.SameMethod;
        return refundMethod.Trim().ToLowerInvariant() switch
        {
            "coupon" => RefundCompensationMethod.Coupon,
            "manual" => RefundCompensationMethod.Manual,
            _ => RefundCompensationMethod.SameMethod,
        };
    }

    public void Process()
    {
        Status = PaymentStatus.Refunded;
        LifecycleStatus = RefundStatus.Succeeded;
        SucceededAtUtc = DateTime.UtcNow;
    }

    public void MarkProviderRefundIssued(string providerName, string? providerRefundId, string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_NAME", "Provider name is required.");
        }

        ProviderName = providerName.Trim();
        ProviderRefundId = string.IsNullOrWhiteSpace(providerRefundId) ? null : providerRefundId.Trim();
        RawProviderResponse = rawResponse;
        LifecycleStatus = RefundStatus.Processing;
    }

    public void UpdateDecision(decimal amount, string? reason, string? refundMethod, string? costBearer, Guid? orderSupportCaseId = null)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_AMOUNT", "Refund amount must be greater than zero.");
        }
        if (string.IsNullOrWhiteSpace(costBearer))
        {
            throw new BusinessRuleException("REFUND_COST_BEARER_REQUIRED", "Refund cost bearer is required.");
        }

        Amount = amount;
        ApprovedAmount = amount;
        Reason = reason?.Trim();
        RefundMethod = string.IsNullOrWhiteSpace(refundMethod) ? null : refundMethod.Trim();
        CostBearer = costBearer.Trim();
        OrderSupportCaseId = orderSupportCaseId;
        CompensationMethod = ResolveCompensationMethod(refundMethod);
    }

    public void Fail(string? failureReason = null)
    {
        Status = PaymentStatus.Failed;
        LifecycleStatus = RefundStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            RawProviderResponse = failureReason;
        }
    }

    public void Cancel()
    {
        Status = PaymentStatus.Cancelled;
        LifecycleStatus = RefundStatus.Cancelled;
    }
}
