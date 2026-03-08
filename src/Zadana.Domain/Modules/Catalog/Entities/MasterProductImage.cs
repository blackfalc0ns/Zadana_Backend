namespace Zadana.Domain.Modules.Catalog.Entities;

public class MasterProductImage
{
    public Guid MasterProductId { get; private set; }
    public string Url { get; private set; } = null!;
    public string? AltText { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    // Navigation
    public MasterProduct MasterProduct { get; private set; } = null!;

    private MasterProductImage() { }

    public MasterProductImage(Guid masterProductId, string url, string? altText = null, int displayOrder = 0, bool isPrimary = false)
    {
        MasterProductId = masterProductId;
        Url = url.Trim();
        AltText = altText?.Trim();
        DisplayOrder = displayOrder;
        IsPrimary = isPrimary;
    }

    public void UpdateMetadata(string? altText)
    {
        AltText = altText?.Trim();
    }

    public void SetDisplayOrder(int order) => DisplayOrder = order;
    
    public void SetAsPrimary() => IsPrimary = true;
    public void RemovePrimary() => IsPrimary = false;
}
