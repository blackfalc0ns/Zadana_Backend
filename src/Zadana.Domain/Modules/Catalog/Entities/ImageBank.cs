using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class ImageBank : BaseEntity
{
    public string Url { get; private set; } = null!;
    public string? AltText { get; private set; }
    public string? Tags { get; private set; }

    // Navigation
    public ICollection<MasterProductImage> ProductUsages { get; private set; } = [];

    private ImageBank() { }

    public ImageBank(string url, string? altText = null, string? tags = null)
    {
        Url = url.Trim();
        AltText = altText?.Trim();
        Tags = tags?.Trim();
    }

    public void UpdateMetadata(string? altText, string? tags)
    {
        AltText = altText?.Trim();
        Tags = tags?.Trim();
    }
}
