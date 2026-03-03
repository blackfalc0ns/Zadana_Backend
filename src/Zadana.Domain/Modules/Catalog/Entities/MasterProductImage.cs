namespace Zadana.Domain.Modules.Catalog.Entities;

public class MasterProductImage
{
    public Guid MasterProductId { get; private set; }
    public Guid ImageBankId { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    // Navigation
    public MasterProduct MasterProduct { get; private set; } = null!;
    public ImageBank ImageBank { get; private set; } = null!;

    private MasterProductImage() { }

    public MasterProductImage(Guid masterProductId, Guid imageBankId, int displayOrder = 0, bool isPrimary = false)
    {
        MasterProductId = masterProductId;
        ImageBankId = imageBankId;
        DisplayOrder = displayOrder;
        IsPrimary = isPrimary;
    }

    public void SetDisplayOrder(int order) => DisplayOrder = order;
    
    public void SetAsPrimary() => IsPrimary = true;
    public void RemovePrimary() => IsPrimary = false;
}
