namespace Zadana.Infrastructure.Services;

public class TwilioSettings
{
    public const string SectionName = "TwilioSettings";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}
