using Zadana.SharedKernel.Primitives;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class UnitOfMeasure : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? Symbol { get; private set; }
    public UnitKind Kind { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];
    public ICollection<MasterProduct> MeasurementMasterProducts { get; private set; } = [];
    public ICollection<MasterProduct> PackageTypeMasterProducts { get; private set; } = [];

    private UnitOfMeasure() { }

    public UnitOfMeasure(string nameAr, string nameEn, string? symbol = null, UnitKind kind = UnitKind.Measurement)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Symbol = symbol?.Trim();
        Kind = kind;
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, string? symbol, UnitKind kind)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Symbol = symbol?.Trim();
        Kind = kind;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
