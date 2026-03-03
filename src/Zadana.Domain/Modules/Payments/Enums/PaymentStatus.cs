namespace Zadana.Domain.Modules.Payments.Enums;

public enum PaymentStatus
{
    Initiated,
    Pending,
    Paid,
    Failed,
    Cancelled,
    Refunded,
    PartiallyRefunded,
    PendingCollection,
    Collected,
    Settled
}
