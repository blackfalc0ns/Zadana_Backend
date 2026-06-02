namespace Zadana.Infrastructure.Settings;

public sealed class NabdaOtpSettings
{
    public const string SectionName = "NabdaOtp";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.nabdaotp.com";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultCountryCode { get; set; } = "+20";
    public string MessageTemplateAr { get; set; } = "رمز التحقق من زادنا هو {0}. لا تشاركه مع أي شخص.";
    public string MessageTemplateEn { get; set; } = "Your Zadana verification code is {0}. Do not share it with anyone.";
    public string WebhookSecret { get; set; } = string.Empty;
}
