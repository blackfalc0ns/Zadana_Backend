using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Social.Entities;

public class AdminAlertEvent : BaseEntity
{
    public string Type { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public string Priority { get; private set; } = null!;
    public string TitleAr { get; private set; } = null!;
    public string TitleEn { get; private set; } = null!;
    public string BodyAr { get; private set; } = null!;
    public string BodyEn { get; private set; } = null!;
    public Guid? ReferenceId { get; private set; }
    public string TargetUrl { get; private set; } = null!;
    public string DataJson { get; private set; } = null!;
    public string DedupeKey { get; private set; } = null!;
    public bool SuppressPush { get; private set; }
    public AdminAlertEventStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public ICollection<AdminAlertDispatch> Dispatches { get; private set; } = new List<AdminAlertDispatch>();

    private AdminAlertEvent()
    {
    }

    public AdminAlertEvent(
        string type,
        string category,
        string priority,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        Guid? referenceId,
        string targetUrl,
        string dataJson,
        string dedupeKey,
        bool suppressPush = false)
    {
        Type = type.Trim();
        Category = category.Trim();
        Priority = priority.Trim();
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        BodyAr = bodyAr.Trim();
        BodyEn = bodyEn.Trim();
        ReferenceId = referenceId;
        TargetUrl = targetUrl.Trim();
        DataJson = dataJson;
        DedupeKey = dedupeKey.Trim();
        SuppressPush = suppressPush;
        Status = AdminAlertEventStatus.Pending;
        NextAttemptAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessing()
    {
        Status = AdminAlertEventStatus.Processing;
        Attempts++;
        LastAttemptAtUtc = DateTime.UtcNow;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = AdminAlertEventStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        NextAttemptAtUtc = null;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error, DateTime nextAttemptAtUtc, int maxAttempts)
    {
        Status = Attempts >= maxAttempts
            ? AdminAlertEventStatus.DeadLetter
            : AdminAlertEventStatus.FailedRetryable;
        LastError = Truncate(error, 2000);
        NextAttemptAtUtc = Status == AdminAlertEventStatus.DeadLetter ? null : nextAttemptAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

