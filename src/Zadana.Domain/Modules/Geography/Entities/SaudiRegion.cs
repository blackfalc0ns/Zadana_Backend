using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Geography.Entities;

public class SaudiRegion : BaseEntity
{
    public string Code { get; private set; } = null!;
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int MapZoom { get; private set; }
    public int SortOrder { get; private set; }

    // Navigation
    public ICollection<SaudiCity> Cities { get; private set; } = [];

    private SaudiRegion() { }

    public SaudiRegion(
        Guid id,
        string code,
        string nameAr,
        string nameEn,
        double latitude,
        double longitude,
        int mapZoom,
        int sortOrder)
    {
        Id = id;
        Code = code.Trim().ToUpperInvariant();
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Latitude = latitude;
        Longitude = longitude;
        MapZoom = mapZoom;
        SortOrder = sortOrder;
    }
}
