namespace Zadana.Infrastructure.Settings;

public sealed class WapilotOtpSettings
{
    public const string SectionName = "WapilotOtp";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.wapilot.net";
    public string SendMessagePath { get; set; } = "/api/v2/{instance_id}/send-message";
    public string InstanceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultCountryCode { get; set; } = "+20";
    public string MessageTemplateAr { get; set; } = "رمز تحقق زادنا:\n```{0}```\n\nلا تشارك هذا الرمز مع أي شخص.";
    public string MessageTemplateEn { get; set; } = "ZADANA verification code:\n```{0}```\n\nDo not share this code with anyone.";
    public string WebhookSecret { get; set; } = string.Empty;
}
