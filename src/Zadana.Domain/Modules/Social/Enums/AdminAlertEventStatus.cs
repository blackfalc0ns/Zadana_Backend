namespace Zadana.Domain.Modules.Social.Enums;

public enum AdminAlertEventStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    FailedRetryable = 3,
    DeadLetter = 4
}

