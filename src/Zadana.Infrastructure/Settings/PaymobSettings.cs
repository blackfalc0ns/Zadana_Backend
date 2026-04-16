namespace Zadana.Infrastructure.Settings;

public class PaymobSettings
{
    public const string SectionName = "Paymob";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public int IframeId { get; set; }
    public int IntegrationId { get; set; }
    public string HmacSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://accept.paymob.com";
    public string Currency { get; set; } = "EGP";
    public string CallbackUrl { get; set; } = string.Empty;
    public int PaymentKeyExpirationSeconds { get; set; } = 3600;
}
