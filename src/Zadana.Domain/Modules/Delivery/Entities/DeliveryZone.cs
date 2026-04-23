using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryZone : BaseEntity
{
    public string City { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public decimal CenterLat { get; private set; }
    public decimal CenterLng { get; private set; }
    public decimal RadiusKm { get; private set; }
    public bool IsActive { get; private set; }

    private DeliveryZone() { }

    public DeliveryZone(
        string city,
        string name,
        decimal centerLat,
        decimal centerLng,
        decimal radiusKm)
    {
        City = city.Trim();
        Name = name.Trim();
        CenterLat = centerLat;
        CenterLng = centerLng;
        RadiusKm = radiusKm;
        IsActive = true;
    }

    public void Update(string city, string name, decimal centerLat, decimal centerLng, decimal radiusKm)
    {
        City = city.Trim();
        Name = name.Trim();
        CenterLat = centerLat;
        CenterLng = centerLng;
        RadiusKm = radiusKm;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
