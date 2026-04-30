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

    // Navigation
    public Payment Payment { get; private set; } = null!;

    private Refund() { }

    public Refund(Guid paymentId, decimal amount, string? reason = null, string? refundMethod = null, string? costBearer = null, Guid? orderSupportCaseId = null)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Refund amount must be greater than zero.");

        PaymentId = paymentId;
        OrderSupportCaseId = orderSupportCaseId;
        Amount = amount;
        Reason = reason?.Trim();
        RefundMethod = string.IsNullOrWhiteSpace(refundMethod) ? null : refundMethod.Trim();
        CostBearer = string.IsNullOrWhiteSpace(costBearer) ? null : costBearer.Trim();
        Status = PaymentStatus.Initiated;
    }

    public void Process()
    {
        Status = PaymentStatus.Refunded;
    }

    public void UpdateDecision(decimal amount, string? reason, string? refundMethod, string? costBearer, Guid? orderSupportCaseId = null)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_AMOUNT", "Refund amount must be greater than zero.");
        }

        Amount = amount;
        Reason = reason?.Trim();
        RefundMethod = string.IsNullOrWhiteSpace(refundMethod) ? null : refundMethod.Trim();
        CostBearer = string.IsNullOrWhiteSpace(costBearer) ? null : costBearer.Trim();
        OrderSupportCaseId = orderSupportCaseId;
    }

    public void Fail()
    {
        Status = PaymentStatus.Failed;
    }
}
