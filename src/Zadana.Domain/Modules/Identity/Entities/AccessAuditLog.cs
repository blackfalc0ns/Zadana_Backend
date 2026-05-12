namespace Zadana.Domain.Modules.Identity.Entities;

public class AccessAuditLog
{
    public Guid Id { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public User TargetUser { get; private set; } = null!;

    private AccessAuditLog() { }

    public AccessAuditLog(
        Guid? actorUserId,
        Guid targetUserId,
        string action,
        string summary,
        string? beforeJson = null,
        string? afterJson = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        Action = action.Trim();
        Summary = summary.Trim();
        BeforeJson = string.IsNullOrWhiteSpace(beforeJson) ? null : beforeJson;
        AfterJson = string.IsNullOrWhiteSpace(afterJson) ? null : afterJson;
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
