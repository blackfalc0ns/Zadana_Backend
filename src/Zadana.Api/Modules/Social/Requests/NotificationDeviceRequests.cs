namespace Zadana.Api.Modules.Social.Requests;

public record RegisterNotificationDeviceRequest(
    string DeviceToken,
    string Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion,
    string? Locale,
    bool NotificationsEnabled = true,
    bool DispatchPushEnabled = true,
    bool AssignmentPushEnabled = true,
    bool SupportPushEnabled = true,
    bool WalletPushEnabled = true,
    bool AccountPushEnabled = true);

public record UpdateNotificationDevicePreferencesRequest(
    string? DeviceId,
    string? DeviceToken,
    bool NotificationsEnabled,
    bool? DispatchPushEnabled = null,
    bool? AssignmentPushEnabled = null,
    bool? SupportPushEnabled = null,
    bool? WalletPushEnabled = null,
    bool? AccountPushEnabled = null);

public record UnregisterNotificationDeviceRequest(
    string? DeviceId,
    string? DeviceToken);
