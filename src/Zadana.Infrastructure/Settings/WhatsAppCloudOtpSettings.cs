namespace Zadana.Infrastructure.Settings;

public sealed class WhatsAppCloudOtpSettings
{
    public const string SectionName = "WhatsAppCloudOtp";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    public string GraphVersion { get; set; } = "v23.0";
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string DefaultCountryCode { get; set; } = "+20";
    public string TemplateName { get; set; } = "zadana_otp_copy_code";
    public string LanguageCode { get; set; } = "en_US";
    public int CopyCodeButtonIndex { get; set; }
}
