namespace Zadana.Infrastructure.Settings;

public class ImageKitSettings
{
    public const string SectionName = "ImageKit";

    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string UrlEndpoint { get; set; } = string.Empty;
}
