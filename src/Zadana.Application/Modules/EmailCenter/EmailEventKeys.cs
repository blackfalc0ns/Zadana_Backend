namespace Zadana.Application.Modules.EmailCenter;

public static class EmailEventKeys
{
    public const string CustomerOrderConfirmed = "customer_order_confirmed";
    public const string CustomerOrderOutForDelivery = "customer_order_out_for_delivery";
    public const string CustomerOrderImportantUpdate = "customer_order_important_update";
    public const string VendorApproved = "vendor_approved";
    public const string VendorOrderActionRequired = "vendor_order_action_required";
    public const string VendorWeeklySummary = "vendor_weekly_summary";
    public const string DriverVerificationUpdate = "driver_verification_update";

    // Security/access emails remain outside the reduced operational email count.
    public const string VendorPasswordReset = "vendor_password_reset";

    public static readonly ISet<string> ReducedOperationalEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CustomerOrderConfirmed,
        CustomerOrderOutForDelivery,
        CustomerOrderImportantUpdate,
        VendorApproved,
        VendorOrderActionRequired,
        VendorWeeklySummary,
        DriverVerificationUpdate
    };

    public static readonly ISet<string> LiveEmailEvents = new HashSet<string>(ReducedOperationalEvents, StringComparer.OrdinalIgnoreCase)
    {
        VendorPasswordReset
    };
}
