using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class UnitOfMeasure : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? Symbol { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private UnitOfMeasure() { }

    public UnitOfMeasure(string nameAr, string nameEn, string? symbol = null)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Symbol = symbol?.Trim();
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, string? symbol)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Symbol = symbol?.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
