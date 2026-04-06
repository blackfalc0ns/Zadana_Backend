using System.ComponentModel.DataAnnotations;

namespace Zadana.Infrastructure.Services;

public class TwilioSettings
{
    public const string SectionName = "TwilioSettings";

    [Required]
    public string AccountSid { get; set; } = string.Empty;
    [Required]
    public string AuthToken { get; set; } = string.Empty;
    [Required]
    [Phone]
    public string FromNumber { get; set; } = string.Empty;
}
