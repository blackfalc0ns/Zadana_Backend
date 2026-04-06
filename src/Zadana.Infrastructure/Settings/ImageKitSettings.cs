using System.ComponentModel.DataAnnotations;

namespace Zadana.Infrastructure.Settings;

public class ImageKitSettings
{
    public const string SectionName = "ImageKit";

    [Required]
    public string PublicKey { get; set; } = string.Empty;
    [Required]
    public string PrivateKey { get; set; } = string.Empty;
    [Required]
    [Url]
    public string UrlEndpoint { get; set; } = string.Empty;
}
