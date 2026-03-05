using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class Brand : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private Brand() { }

    public Brand(string nameAr, string nameEn, string? logoUrl = null)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, string? logoUrl)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
