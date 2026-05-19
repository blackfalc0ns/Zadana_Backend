namespace Zadana.Infrastructure.Settings;

/// <summary>
/// Configuration for the Moyasar payment gateway. Bound from the
/// <c>"Moyasar"</c> section of <c>appsettings.json</c>.
/// </summary>
public class MoyasarSettings
{
    public const string SectionName = "Moyasar";

    /// <summary>Master switch. Moyasar is wired up and exposed only when this is true.</summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.moyasar.com/v1/";

    /// <summary>Public publishable key sent to mobile/web SDKs.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>Secret key used by the backend for fetch/refund/server-to-server calls.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Shared secret used to validate inbound webhooks.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>URL the customer is redirected to after the Moyasar form completes.</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>Methods enabled in the embedded form: creditcard, applepay, samsungpay, stcpay.</summary>
    public string[] EnabledMethods { get; set; } = ["creditcard", "applepay", "samsungpay", "stcpay"];

    /// <summary>Card networks accepted by the form.</summary>
    public string[] SupportedNetworks { get; set; } = ["mada", "visa", "mastercard"];

    /// <summary>Currency the gateway is allowed to accept. Must be SAR.</summary>
    public string Currency { get; set; } = "SAR";

    /// <summary>Outbound transfer settings for Moyasar Payouts.</summary>
    public MoyasarPayoutSettings Payouts { get; set; } = new();
}

public class MoyasarPayoutSettings
{
    public bool Enabled { get; set; }

    public string SourceId { get; set; } = string.Empty;

    public string DefaultCountry { get; set; } = "SA";

    public string DefaultCity { get; set; } = "Riyadh";

    public string VendorPurpose { get; set; } = "payment_to_merchant";

    public string DriverPurpose { get; set; } = "payroll_benefits";

    public int PollingIntervalSeconds { get; set; } = 300;

    public int UnknownRetryDelaySeconds { get; set; } = 120;

    public int ProcessingAlertAfterMinutes { get; set; } = 60;
}
