using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

public class UserPermissionOverride
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionKey { get; private set; } = null!;
    public PermissionOverrideMode Mode { get; private set; }
    public bool IsActive { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    private UserPermissionOverride() { }

    public UserPermissionOverride(Guid userId, string permissionKey, PermissionOverrideMode mode, string? reason = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        PermissionKey = permissionKey.Trim();
        Mode = mode;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(PermissionOverrideMode mode, string? reason, bool isActive)
    {
        Mode = mode;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
