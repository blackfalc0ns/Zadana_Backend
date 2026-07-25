using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Orders.Requests;

public record UpsertPlatformPickupSettingsRequest(
    [property: JsonPropertyName("delivery_option_enabled")] bool DeliveryOptionEnabled,
    [property: JsonPropertyName("pickup_option_enabled")] bool PickupOptionEnabled,
    [property: JsonPropertyName("pickup_cash_on_pickup_enabled")] bool PickupCashOnPickupEnabled,
    [property: JsonPropertyName("pickup_commission_percent")] decimal PickupCommissionPercent,
    [property: JsonPropertyName("pickup_no_show_timeout_hours")] int PickupNoShowTimeoutHours,
    [property: JsonPropertyName("pickup_otp_max_attempts")] int PickupOtpMaxAttempts,
    [property: JsonPropertyName("pickup_otp_lockout_minutes")] int PickupOtpLockoutMinutes);

public record PlatformPickupSettingsDto(
    [property: JsonPropertyName("delivery_option_enabled")] bool DeliveryOptionEnabled,
    [property: JsonPropertyName("pickup_option_enabled")] bool PickupOptionEnabled,
    [property: JsonPropertyName("pickup_cash_on_pickup_enabled")] bool PickupCashOnPickupEnabled,
    [property: JsonPropertyName("pickup_commission_percent")] decimal PickupCommissionPercent,
    [property: JsonPropertyName("pickup_no_show_timeout_hours")] int PickupNoShowTimeoutHours,
    [property: JsonPropertyName("pickup_otp_max_attempts")] int PickupOtpMaxAttempts,
    [property: JsonPropertyName("pickup_otp_lockout_minutes")] int PickupOtpLockoutMinutes,
    [property: JsonPropertyName("updated_at_utc")] DateTime? UpdatedAtUtc);
