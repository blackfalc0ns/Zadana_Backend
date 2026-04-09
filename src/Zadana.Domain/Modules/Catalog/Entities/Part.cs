using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class Part : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public Guid ProductTypeId { get; private set; }
    public bool IsActive { get; private set; }

    public ProductType ProductType { get; private set; } = null!;
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private Part() { }

    public Part(string nameAr, string nameEn, Guid productTypeId)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        ProductTypeId = productTypeId;
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, Guid productTypeId)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        ProductTypeId = productTypeId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
