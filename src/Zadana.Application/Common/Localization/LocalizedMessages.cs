using System.Globalization;
using System.Resources;

namespace Zadana.Application.Common.Localization;

/// <summary>
/// Provides bilingual (Arabic + English) message resolution from SharedResource .resx files.
/// Used for success/action messages that need both languages simultaneously in the API response.
/// </summary>
public static class LocalizedMessages
{
    private static readonly ResourceManager ResourceManager =
        new("Zadana.Application.Common.Localization.SharedResource",
            typeof(SharedResource).Assembly);

    private static readonly CultureInfo ArCulture = new("ar");
    private static readonly CultureInfo EnCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets the Arabic translation for the given resource key.
    /// </summary>
    public static string GetAr(string key) =>
        ResourceManager.GetString(key, ArCulture) ?? key;

    /// <summary>
    /// Gets the English (default) translation for the given resource key.
    /// </summary>
    public static string GetEn(string key) =>
        ResourceManager.GetString(key, EnCulture) ?? key;

    /// <summary>
    /// Gets both Arabic and English translations as a tuple.
    /// </summary>
    public static (string Ar, string En) Get(string key) =>
        (GetAr(key), GetEn(key));

    // ── Success Message Keys ────────────────────────────────────────

    // Checkout
    public const string OrderPlacedSuccess = "ORDER_PLACED_SUCCESS";
    public const string PromoCodeApplied = "PROMO_CODE_APPLIED";
    public const string PromoCodeRemoved = "PROMO_CODE_REMOVED";

    // Cart
    public const string CartItemAdded = "CART_ITEM_ADDED";
    public const string CartItemUpdated = "CART_ITEM_UPDATED";
    public const string CartItemRemoved = "CART_ITEM_REMOVED";
    public const string CartCleared = "CART_CLEARED";

    // Driver - Availability & Location
    public const string DriverAvailabilityOn = "DRIVER_AVAILABILITY_ON";
    public const string DriverAvailabilityOff = "DRIVER_AVAILABILITY_OFF";
    public const string DriverLocationUpdated = "DRIVER_LOCATION_UPDATED";

    // Driver - Arrival
    public const string DriverArrivedAtVendor = "DRIVER_ARRIVED_AT_VENDOR";
    public const string DriverArrivedAtCustomer = "DRIVER_ARRIVED_AT_CUSTOMER";

    // Driver - Proof
    public const string DeliveryProofSubmitted = "DELIVERY_PROOF_SUBMITTED";

    // Driver - OTP
    public const string PickupOtpVerified = "PICKUP_OTP_VERIFIED";
    public const string DeliveryOtpVerified = "DELIVERY_OTP_VERIFIED";
    public const string PickupOtpAlreadyVerified = "PICKUP_OTP_ALREADY_VERIFIED";
    public const string DeliveryOtpAlreadyVerified = "DELIVERY_OTP_ALREADY_VERIFIED";

    // Driver - Order Status
    public const string OrderStatusUpdated = "ORDER_STATUS_UPDATED";

    // Driver - Offer
    public const string OfferAccepted = "OFFER_ACCEPTED";
    public const string OfferRejected = "OFFER_REJECTED";

    // Notifications
    public const string NotificationMarkedRead = "NOTIFICATION_MARKED_READ";
    public const string AllNotificationsMarkedRead = "ALL_NOTIFICATIONS_MARKED_READ";

    // Payments
    public const string PaymentConfirmedSuccess = "PAYMENT_CONFIRMED_SUCCESS";
    public const string PaymentAlreadyConfirmed = "PAYMENT_ALREADY_CONFIRMED";
    public const string PaymentStillPending = "PAYMENT_STILL_PENDING";
    public const string PaymentConfirmationFailed = "PAYMENT_CONFIRMATION_FAILED";
}
