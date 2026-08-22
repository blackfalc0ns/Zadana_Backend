using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorBranch : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public string AddressLine { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string ContactPhone { get; private set; } = null!;
    public string ManagerName { get; private set; } = null!;
    public string ManagerContact { get; private set; } = null!;
    public decimal DeliveryRadiusKm { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public Vendor Vendor { get; private set; } = null!;
    public ICollection<BranchOperatingHour> OperatingHours { get; private set; } = [];

    private VendorBranch() { }

    public VendorBranch(
        Guid vendorId,
        string name,
        string code,
        bool isPrimary,
        string addressLine,
        string region,
        string city,
        decimal latitude,
        decimal longitude,
        string contactPhone,
        string managerName,
        string managerContact,
        decimal deliveryRadiusKm)
    {
        if (latitude < -90 || latitude > 90)
            throw new InvalidOperationException("Latitude must be between -90 and 90.");
        if (longitude < -180 || longitude > 180)
            throw new InvalidOperationException("Longitude must be between -180 and 180.");
        if (deliveryRadiusKm <= 0)
            throw new InvalidOperationException("Delivery radius must be greater than zero.");

        VendorId = vendorId;
        Name = name.Trim();
        Code = code.Trim();
        IsPrimary = isPrimary;
        AddressLine = addressLine.Trim();
        Region = region.Trim();
        City = city.Trim();
        Latitude = latitude;
        Longitude = longitude;
        ContactPhone = contactPhone.Trim();
        ManagerName = managerName.Trim();
        ManagerContact = managerContact.Trim();
        DeliveryRadiusKm = deliveryRadiusKm;
        IsActive = true;
    }

    public VendorBranch(
        Guid vendorId,
        string name,
        string addressLine,
        decimal latitude,
        decimal longitude,
        string contactPhone,
        decimal deliveryRadiusKm)
        : this(
            vendorId,
            name,
            name,
            false,
            addressLine,
            string.Empty,
            string.Empty,
            latitude,
            longitude,
            contactPhone,
            string.Empty,
            string.Empty,
            deliveryRadiusKm)
    {
    }

    public void Update(
        string name,
        string code,
        bool isPrimary,
        string addressLine,
        string region,
        string city,
        decimal latitude,
        decimal longitude,
        string contactPhone,
        string managerName,
        string managerContact,
        decimal deliveryRadiusKm)
    {
        Name = name.Trim();
        Code = code.Trim();
        IsPrimary = isPrimary;
        AddressLine = addressLine.Trim();
        Region = region.Trim();
        City = city.Trim();
        Latitude = latitude;
        Longitude = longitude;
        ContactPhone = contactPhone.Trim();
        ManagerName = managerName.Trim();
        ManagerContact = managerContact.Trim();
        DeliveryRadiusKm = deliveryRadiusKm;
    }

    public void Update(
        string name,
        string addressLine,
        decimal latitude,
        decimal longitude,
        string contactPhone,
        decimal deliveryRadiusKm)
    {
        Update(
            name,
            Code,
            IsPrimary,
            addressLine,
            Region,
            City,
            latitude,
            longitude,
            contactPhone,
            ManagerName,
            ManagerContact,
            deliveryRadiusKm);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Branch name is required.");

        Name = name.Trim();
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
