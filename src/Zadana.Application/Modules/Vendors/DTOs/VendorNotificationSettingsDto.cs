namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorNotificationSettingsDto(
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    bool NewOrdersNotificationsEnabled);
