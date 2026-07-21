namespace Zadana.Domain.Modules.Wallets.Enums;

public enum PayoutStatus
{
    Pending,
    Queued,
    Processing,
    Paid,
    Reversed,
    Failed,
    Cancelled
}
