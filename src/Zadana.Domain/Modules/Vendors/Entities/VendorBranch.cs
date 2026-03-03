using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorBranch : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = null!;
    public string AddressLine { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string ContactPhone { get; private set; } = null!;
    public decimal DeliveryRadiusKm { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public Vendor Vendor { get; private set; } = null!;
    public ICollection<BranchOperatingHour> OperatingHours { get; private set; } = [];

    private VendorBranch() { }

    public VendorBranch(
        Guid vendorId,
        string name,
        string addressLine,
        decimal latitude,
        decimal longitude,
        string contactPhone,
        decimal deliveryRadiusKm)
    {
        VendorId = vendorId;
        Name = name.Trim();
        AddressLine = addressLine.Trim();
        Latitude = latitude;
        Longitude = longitude;
        ContactPhone = contactPhone.Trim();
        DeliveryRadiusKm = deliveryRadiusKm;
        IsActive = true;
    }

    public void Update(
        string name,
        string addressLine,
        decimal latitude,
        decimal longitude,
        string contactPhone,
        decimal deliveryRadiusKm)
    {
        Name = name.Trim();
        AddressLine = addressLine.Trim();
        Latitude = latitude;
        Longitude = longitude;
        ContactPhone = contactPhone.Trim();
        DeliveryRadiusKm = deliveryRadiusKm;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
