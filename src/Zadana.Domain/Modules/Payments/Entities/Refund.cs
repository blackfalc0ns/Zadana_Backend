using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Payments.Entities;

public class Refund : BaseEntity
{
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reason { get; private set; }
    public PaymentStatus Status { get; private set; }

    // Navigation
    public Payment Payment { get; private set; } = null!;

    private Refund() { }

    public Refund(Guid paymentId, decimal amount, string? reason = null)
    {
        if (amount <= 0) throw new BusinessRuleException("INVALID_AMOUNT", "Refund amount must be greater than zero.");

        PaymentId = paymentId;
        Amount = amount;
        Reason = reason?.Trim();
        Status = PaymentStatus.Initiated;
    }

    public void Process()
    {
        Status = PaymentStatus.Refunded;
    }

    public void Fail()
    {
        Status = PaymentStatus.Failed;
    }
}
