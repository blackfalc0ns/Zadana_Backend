using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class UserPushDevice : BaseEntity
{
    public Guid UserId { get; private set; }
    public string DeviceToken { get; private set; } = null!;
    public PushPlatform Platform { get; private set; }
    public string? DeviceId { get; private set; }
    public string? DeviceName { get; private set; }
    public string? AppVersion { get; private set; }
    public string? Locale { get; private set; }
    public bool NotificationsEnabled { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime LastRegisteredAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    private UserPushDevice()
    {
    }

    public UserPushDevice(
        Guid userId,
        string deviceToken,
        PushPlatform platform,
        string? deviceId,
        string? deviceName,
        string? appVersion,
        string? locale,
        bool notificationsEnabled)
    {
        UserId = userId;
        DeviceToken = deviceToken.Trim();
        Platform = platform;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        AppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();
        Locale = string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();
        NotificationsEnabled = notificationsEnabled;
        IsActive = true;
        LastRegisteredAtUtc = DateTime.UtcNow;
        LastSeenAtUtc = DateTime.UtcNow;
    }

    public void Register(
        Guid userId,
        string deviceToken,
        PushPlatform platform,
        string? deviceId,
        string? deviceName,
        string? appVersion,
        string? locale,
        bool notificationsEnabled)
    {
        UserId = userId;
        DeviceToken = deviceToken.Trim();
        Platform = platform;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        AppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();
        Locale = string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();
        NotificationsEnabled = notificationsEnabled;
        IsActive = true;
        LastRegisteredAtUtc = DateTime.UtcNow;
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateNotificationsEnabled(bool notificationsEnabled)
    {
        NotificationsEnabled = notificationsEnabled;
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Touch()
    {
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
