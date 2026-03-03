using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Payments.Entities;

public class Payment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public PaymentMethodType Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;
    public ICollection<Refund> Refunds { get; private set; } = [];

    private Payment() { }

    public Payment(Guid orderId, PaymentMethodType method, decimal amount)
    {
        if (amount < 0) throw new BusinessRuleException("INVALID_AMOUNT", "Payment amount cannot be negative.");

        OrderId = orderId;
        Method = method;
        Amount = amount;
        Status = PaymentStatus.Initiated;
    }

    public void MarkAsPending(string providerName, string transactionId)
    {
        ProviderName = providerName.Trim();
        ProviderTransactionId = transactionId.Trim();
        Status = PaymentStatus.Pending;
    }

    public void MarkAsPaid()
    {
        Status = PaymentStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        Order?.UpdatePaymentStatus(Status);
    }

    public void MarkAsFailed(string? failureReason)
    {
        Status = PaymentStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        Order?.UpdatePaymentStatus(Status);
    }
}
