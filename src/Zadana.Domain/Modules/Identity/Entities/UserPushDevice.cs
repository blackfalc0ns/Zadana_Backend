using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Support;
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
    public bool AdminDriversPushEnabled { get; private set; }
    public bool AdminVendorsPushEnabled { get; private set; }
    public bool AdminCatalogPushEnabled { get; private set; }
    public bool AdminDisputesPushEnabled { get; private set; }
    public bool AdminRefundsPushEnabled { get; private set; }
    public bool AdminSettlementsPushEnabled { get; private set; }
    public bool AdminSupportPushEnabled { get; private set; }
    public bool AdminSystemPushEnabled { get; private set; }
    public string NotificationSound { get; private set; } = NotificationSoundCatalog.Classic;
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
        bool accountPushEnabled = true,
        bool adminDriversPushEnabled = true,
        bool adminVendorsPushEnabled = true,
        bool adminCatalogPushEnabled = true,
        bool adminDisputesPushEnabled = true,
        bool adminRefundsPushEnabled = true,
        bool adminSettlementsPushEnabled = true,
        bool adminSupportPushEnabled = true,
        bool adminSystemPushEnabled = true,
        string? notificationSound = null)
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
        AdminDriversPushEnabled = adminDriversPushEnabled;
        AdminVendorsPushEnabled = adminVendorsPushEnabled;
        AdminCatalogPushEnabled = adminCatalogPushEnabled;
        AdminDisputesPushEnabled = adminDisputesPushEnabled;
        AdminRefundsPushEnabled = adminRefundsPushEnabled;
        AdminSettlementsPushEnabled = adminSettlementsPushEnabled;
        AdminSupportPushEnabled = adminSupportPushEnabled;
        AdminSystemPushEnabled = adminSystemPushEnabled;
        NotificationSound = NotificationSoundCatalog.Normalize(notificationSound);
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
        bool accountPushEnabled = true,
        bool adminDriversPushEnabled = true,
        bool adminVendorsPushEnabled = true,
        bool adminCatalogPushEnabled = true,
        bool adminDisputesPushEnabled = true,
        bool adminRefundsPushEnabled = true,
        bool adminSettlementsPushEnabled = true,
        bool adminSupportPushEnabled = true,
        bool adminSystemPushEnabled = true,
        string? notificationSound = null)
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
        AdminDriversPushEnabled = adminDriversPushEnabled;
        AdminVendorsPushEnabled = adminVendorsPushEnabled;
        AdminCatalogPushEnabled = adminCatalogPushEnabled;
        AdminDisputesPushEnabled = adminDisputesPushEnabled;
        AdminRefundsPushEnabled = adminRefundsPushEnabled;
        AdminSettlementsPushEnabled = adminSettlementsPushEnabled;
        AdminSupportPushEnabled = adminSupportPushEnabled;
        AdminSystemPushEnabled = adminSystemPushEnabled;
        NotificationSound = NotificationSoundCatalog.Normalize(notificationSound, NotificationSound);
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
        bool? accountPushEnabled = null,
        bool? adminDriversPushEnabled = null,
        bool? adminVendorsPushEnabled = null,
        bool? adminCatalogPushEnabled = null,
        bool? adminDisputesPushEnabled = null,
        bool? adminRefundsPushEnabled = null,
        bool? adminSettlementsPushEnabled = null,
        bool? adminSupportPushEnabled = null,
        bool? adminSystemPushEnabled = null,
        string? notificationSound = null)
    {
        NotificationsEnabled = notificationsEnabled;
        DispatchPushEnabled = dispatchPushEnabled ?? DispatchPushEnabled;
        AssignmentPushEnabled = assignmentPushEnabled ?? AssignmentPushEnabled;
        SupportPushEnabled = supportPushEnabled ?? SupportPushEnabled;
        WalletPushEnabled = walletPushEnabled ?? WalletPushEnabled;
        AccountPushEnabled = accountPushEnabled ?? AccountPushEnabled;
        AdminDriversPushEnabled = adminDriversPushEnabled ?? AdminDriversPushEnabled;
        AdminVendorsPushEnabled = adminVendorsPushEnabled ?? AdminVendorsPushEnabled;
        AdminCatalogPushEnabled = adminCatalogPushEnabled ?? AdminCatalogPushEnabled;
        AdminDisputesPushEnabled = adminDisputesPushEnabled ?? AdminDisputesPushEnabled;
        AdminRefundsPushEnabled = adminRefundsPushEnabled ?? AdminRefundsPushEnabled;
        AdminSettlementsPushEnabled = adminSettlementsPushEnabled ?? AdminSettlementsPushEnabled;
        AdminSupportPushEnabled = adminSupportPushEnabled ?? AdminSupportPushEnabled;
        AdminSystemPushEnabled = adminSystemPushEnabled ?? AdminSystemPushEnabled;
        NotificationSound = notificationSound == null
            ? NotificationSound
            : NotificationSoundCatalog.Normalize(notificationSound, NotificationSound);
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

    public bool IsAdminPushAllowedForCategory(string? category) =>
        NotificationsEnabled &&
        IsActive &&
        category?.Trim().ToLowerInvariant() switch
        {
            "drivers" => AdminDriversPushEnabled,
            "vendors" => AdminVendorsPushEnabled,
            "catalog" => AdminCatalogPushEnabled,
            "disputes" => AdminDisputesPushEnabled,
            "refunds" => AdminRefundsPushEnabled,
            "settlements" => AdminSettlementsPushEnabled,
            "support" => AdminSupportPushEnabled,
            "system" => AdminSystemPushEnabled,
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
