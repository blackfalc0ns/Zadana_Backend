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
    bool AccountPushEnabled = true,
    bool AdminDriversPushEnabled = true,
    bool AdminVendorsPushEnabled = true,
    bool AdminCatalogPushEnabled = true,
    bool AdminDisputesPushEnabled = true,
    bool AdminRefundsPushEnabled = true,
    bool AdminSettlementsPushEnabled = true,
    bool AdminSupportPushEnabled = true,
    bool AdminSystemPushEnabled = true,
    string? NotificationSound = null);

public record UpdateNotificationDevicePreferencesRequest(
    string? DeviceId,
    string? DeviceToken,
    bool NotificationsEnabled,
    bool? DispatchPushEnabled = null,
    bool? AssignmentPushEnabled = null,
    bool? SupportPushEnabled = null,
    bool? WalletPushEnabled = null,
    bool? AccountPushEnabled = null,
    bool? AdminDriversPushEnabled = null,
    bool? AdminVendorsPushEnabled = null,
    bool? AdminCatalogPushEnabled = null,
    bool? AdminDisputesPushEnabled = null,
    bool? AdminRefundsPushEnabled = null,
    bool? AdminSettlementsPushEnabled = null,
    bool? AdminSupportPushEnabled = null,
    bool? AdminSystemPushEnabled = null,
    string? NotificationSound = null);

public record UnregisterNotificationDeviceRequest(
    string? DeviceId,
    string? DeviceToken);
