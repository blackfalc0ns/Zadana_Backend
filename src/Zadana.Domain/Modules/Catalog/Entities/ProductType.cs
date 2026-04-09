using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class ProductType : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }

    public Category Category { get; private set; } = null!;
    public ICollection<Part> Parts { get; private set; } = [];
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private ProductType() { }

    public ProductType(string nameAr, string nameEn, Guid categoryId)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        CategoryId = categoryId;
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, Guid categoryId)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        CategoryId = categoryId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
