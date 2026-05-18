namespace Zadana.Domain.Modules.Payments.Enums;

/// <summary>
/// Processing state of a webhook/notification stored in
/// <c>PaymentProviderEventInbox</c>.
/// </summary>
public enum PaymentProviderEventStatus
{
    Received = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    Ignored = 4,
}
