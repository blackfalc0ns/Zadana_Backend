using System.ComponentModel.DataAnnotations;

namespace Zadana.Infrastructure.Settings;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "ImageKit";
    public LocalMediaStorageSettings Local { get; set; } = new();
}

public sealed class LocalMediaStorageSettings
{
    /// <summary>
    /// Physical media root. Relative paths are resolved from the API content
    /// root, but an absolute persistent path is strongly recommended.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Public origin mapped to RootPath, for example https://media.zadna0.com.</summary>
    [Url]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool ConvertImagesToWebp { get; set; } = true;
    [Range(1, 100)]
    public int WebpQuality { get; set; } = 82;
    [Range(320, 12000)]
    public int MaxWidth { get; set; } = 2000;
    [Range(320, 12000)]
    public int MaxHeight { get; set; } = 2000;
    [Range(1_000_000, 100_000_000)]
    public long MaxPixelCount { get; set; } = 40_000_000;
    [Range(1, 8)]
    public int MaxConcurrentImageProcessors { get; set; } = 2;
}
