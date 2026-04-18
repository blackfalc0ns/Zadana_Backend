namespace Zadana.Api.Modules.Social.Requests;

public record RegisterNotificationDeviceRequest(
    string DeviceToken,
    string Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion,
    string? Locale,
    bool NotificationsEnabled = true);

public record UpdateNotificationDevicePreferencesRequest(
    string? DeviceId,
    string? DeviceToken,
    bool NotificationsEnabled);

public record UnregisterNotificationDeviceRequest(
    string? DeviceId,
    string? DeviceToken);
