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
    public bool DispatchPushEnabled { get; private set; }
    public bool AssignmentPushEnabled { get; private set; }
    public bool SupportPushEnabled { get; private set; }
    public bool WalletPushEnabled { get; private set; }
    public bool AccountPushEnabled { get; private set; }
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
        bool notificationsEnabled,
        bool dispatchPushEnabled = true,
        bool assignmentPushEnabled = true,
        bool supportPushEnabled = true,
        bool walletPushEnabled = true,
        bool accountPushEnabled = true)
    {
        UserId = userId;
        DeviceToken = deviceToken.Trim();
        Platform = platform;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        AppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();
        Locale = NormalizeLocale(locale);
        NotificationsEnabled = notificationsEnabled;
        DispatchPushEnabled = dispatchPushEnabled;
        AssignmentPushEnabled = assignmentPushEnabled;
        SupportPushEnabled = supportPushEnabled;
        WalletPushEnabled = walletPushEnabled;
        AccountPushEnabled = accountPushEnabled;
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
        bool notificationsEnabled,
        bool dispatchPushEnabled = true,
        bool assignmentPushEnabled = true,
        bool supportPushEnabled = true,
        bool walletPushEnabled = true,
        bool accountPushEnabled = true)
    {
        UserId = userId;
        DeviceToken = deviceToken.Trim();
        Platform = platform;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        AppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();
        Locale = NormalizeLocale(locale);
        NotificationsEnabled = notificationsEnabled;
        DispatchPushEnabled = dispatchPushEnabled;
        AssignmentPushEnabled = assignmentPushEnabled;
        SupportPushEnabled = supportPushEnabled;
        WalletPushEnabled = walletPushEnabled;
        AccountPushEnabled = accountPushEnabled;
        IsActive = true;
        LastRegisteredAtUtc = DateTime.UtcNow;
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateNotificationsEnabled(bool notificationsEnabled)
    {
        UpdatePushPreferences(notificationsEnabled);
    }

    public void UpdatePushPreferences(
        bool notificationsEnabled,
        bool? dispatchPushEnabled = null,
        bool? assignmentPushEnabled = null,
        bool? supportPushEnabled = null,
        bool? walletPushEnabled = null,
        bool? accountPushEnabled = null)
    {
        NotificationsEnabled = notificationsEnabled;
        DispatchPushEnabled = dispatchPushEnabled ?? DispatchPushEnabled;
        AssignmentPushEnabled = assignmentPushEnabled ?? AssignmentPushEnabled;
        SupportPushEnabled = supportPushEnabled ?? SupportPushEnabled;
        WalletPushEnabled = walletPushEnabled ?? WalletPushEnabled;
        AccountPushEnabled = accountPushEnabled ?? AccountPushEnabled;
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsPushAllowedForCategory(string? category) =>
        NotificationsEnabled &&
        IsActive &&
        category?.Trim().ToLowerInvariant() switch
        {
            "dispatch" => DispatchPushEnabled,
            "assignment" => AssignmentPushEnabled,
            "support" => SupportPushEnabled,
            "wallet" => WalletPushEnabled,
            "account" => AccountPushEnabled,
            _ => true
        };

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

    private static string? NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var normalized = locale.Trim().Replace('_', '-');
        if (normalized.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
        {
            return "ar";
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return null;
    }
}
