using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

/// <summary>
/// Singleton platform settings controlling pickup/delivery fulfillment options.
/// </summary>
public sealed class PlatformPickupSettings : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    public bool DeliveryOptionEnabled { get; private set; } = true;
    public bool PickupOptionEnabled { get; private set; } = true;
    public bool PickupCashOnPickupEnabled { get; private set; }
    public decimal PickupCommissionPercent { get; private set; } = 5.0m;
    public int PickupNoShowTimeoutHours { get; private set; } = 24;
    public int PickupOtpMaxAttempts { get; private set; } = 5;
    public int PickupOtpLockoutMinutes { get; private set; } = 30;
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private PlatformPickupSettings()
    {
    }

    public PlatformPickupSettings(Guid? updatedByUserId = null)
    {
        Id = SingletonId;
        DeliveryOptionEnabled = true;
        PickupOptionEnabled = true;
        PickupCashOnPickupEnabled = false;
        PickupCommissionPercent = 5.0m;
        PickupNoShowTimeoutHours = 24;
        PickupOtpMaxAttempts = 5;
        PickupOtpLockoutMinutes = 30;
        UpdatedByUserId = updatedByUserId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        bool deliveryOptionEnabled,
        bool pickupOptionEnabled,
        bool pickupCashOnPickupEnabled,
        decimal pickupCommissionPercent,
        int pickupNoShowTimeoutHours,
        int pickupOtpMaxAttempts,
        int pickupOtpLockoutMinutes,
        Guid? updatedByUserId)
    {
        if (pickupCommissionPercent < 0 || pickupCommissionPercent > 100)
        {
            throw new BusinessRuleException(
                "PICKUP_COMMISSION_INVALID",
                "Pickup commission percent must be between 0 and 100.");
        }

        if (pickupNoShowTimeoutHours < 1 || pickupNoShowTimeoutHours > 168)
        {
            throw new BusinessRuleException(
                "PICKUP_NOSHOW_TIMEOUT_INVALID",
                "Pickup no-show timeout must be between 1 and 168 hours.");
        }

        if (pickupOtpMaxAttempts < 1 || pickupOtpMaxAttempts > 20)
        {
            throw new BusinessRuleException(
                "PICKUP_OTP_MAX_ATTEMPTS_INVALID",
                "Pickup OTP max attempts must be between 1 and 20.");
        }

        if (pickupOtpLockoutMinutes < 1 || pickupOtpLockoutMinutes > 1440)
        {
            throw new BusinessRuleException(
                "PICKUP_OTP_LOCKOUT_INVALID",
                "Pickup OTP lockout minutes must be between 1 and 1440.");
        }

        DeliveryOptionEnabled = deliveryOptionEnabled;
        PickupOptionEnabled = pickupOptionEnabled;
        PickupCashOnPickupEnabled = pickupCashOnPickupEnabled;
        PickupCommissionPercent = Math.Round(pickupCommissionPercent, 2, MidpointRounding.AwayFromZero);
        PickupNoShowTimeoutHours = pickupNoShowTimeoutHours;
        PickupOtpMaxAttempts = pickupOtpMaxAttempts;
        PickupOtpLockoutMinutes = pickupOtpLockoutMinutes;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
