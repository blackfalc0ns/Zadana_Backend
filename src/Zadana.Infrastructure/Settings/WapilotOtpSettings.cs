namespace Zadana.Infrastructure.Settings;

public sealed class WapilotOtpSettings
{
    public const string SectionName = "WapilotOtp";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.wapilot.net/api/v2";
    public string SendMessagePath { get; set; } = "/{instance_id}/send-message";
    public string InstanceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultCountryCode { get; set; } = "+20";
    public string MessageTemplateAr { get; set; } = "رمز التحقق من زادنا هو {0}. لا تشاركه مع أي شخص.";
    public string MessageTemplateEn { get; set; } = "Your Zadana verification code is {0}. Do not share it with anyone.";
    public string WebhookSecret { get; set; } = string.Empty;
}
