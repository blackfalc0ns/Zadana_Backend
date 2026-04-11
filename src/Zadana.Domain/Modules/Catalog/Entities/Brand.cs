using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class Brand : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public Guid? CategoryId { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public Category? Category { get; private set; }
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private Brand() { }

    public Brand(string nameAr, string nameEn, string? logoUrl = null, Guid? categoryId = null)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        CategoryId = categoryId;
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, string? logoUrl, Guid? categoryId)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        CategoryId = categoryId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
