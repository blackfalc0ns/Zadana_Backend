namespace Zadana.Domain.Modules.Social.Enums;

public enum AdminAlertDispatchStatus
{
    Pending = 0,
    Persisted = 1,
    SignalRSent = 2,
    PushSent = 3,
    PushSkipped = 4,
    Failed = 5
}

