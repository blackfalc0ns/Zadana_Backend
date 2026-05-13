using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Social.Entities;

public class AdminAlertDispatch : BaseEntity
{
    public Guid AdminAlertEventId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public Guid? NotificationId { get; private set; }
    public AdminAlertDispatchStatus Status { get; private set; }
    public bool SignalRSent { get; private set; }
    public bool PushAttempted { get; private set; }
    public bool PushSent { get; private set; }
    public bool PushSkipped { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }

    public AdminAlertEvent Event { get; private set; } = null!;

    private AdminAlertDispatch()
    {
    }

    public AdminAlertDispatch(Guid adminAlertEventId, Guid adminUserId)
    {
        AdminAlertEventId = adminAlertEventId;
        AdminUserId = adminUserId;
        Status = AdminAlertDispatchStatus.Pending;
    }

    public void MarkPersisted(Guid notificationId)
    {
        NotificationId = notificationId;
        Status = AdminAlertDispatchStatus.Persisted;
        Attempts++;
        LastAttemptAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSignalRSent()
    {
        SignalRSent = true;
        Status = AdminAlertDispatchStatus.SignalRSent;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPushResult(bool attempted, bool sent, bool skipped, string? error)
    {
        PushAttempted = attempted;
        PushSent = sent;
        PushSkipped = skipped;
        LastError = string.IsNullOrWhiteSpace(error) ? LastError : Truncate(error.Trim(), 1000);
        Status = sent
            ? AdminAlertDispatchStatus.PushSent
            : skipped
                ? AdminAlertDispatchStatus.PushSkipped
                : AdminAlertDispatchStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = AdminAlertDispatchStatus.Failed;
        LastError = Truncate(error, 1000);
        Attempts++;
        LastAttemptAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

