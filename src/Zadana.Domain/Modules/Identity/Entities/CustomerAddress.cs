using Zadana.SharedKernel.Primitives;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

public class CustomerAddress : BaseEntity
{
    public Guid UserId { get; private set; }
    public AddressLabel? Label { get; private set; }
    public string ContactName { get; private set; } = null!;
    public string ContactPhone { get; private set; } = null!;
    public string AddressLine { get; private set; } = null!;
    public string? BuildingNo { get; private set; }
    public string? FloorNo { get; private set; }
    public string? ApartmentNo { get; private set; }
    public string? City { get; private set; }
    public string? Area { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsDefault { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private CustomerAddress() { }

    public CustomerAddress(
        Guid userId,
        string contactName,
        string contactPhone,
        string addressLine,
        AddressLabel? label = null,
        string? buildingNo = null,
        string? floorNo = null,
        string? apartmentNo = null,
        string? city = null,
        string? area = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        UserId = userId;
        ContactName = contactName.Trim();
        ContactPhone = contactPhone.Trim();
        AddressLine = addressLine.Trim();
        Label = label;
        BuildingNo = buildingNo?.Trim();
        FloorNo = floorNo?.Trim();
        ApartmentNo = apartmentNo?.Trim();
        City = city?.Trim();
        Area = area?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        IsDefault = false;
    }

    public void SetAsDefault() => IsDefault = true;
    public void RemoveDefault() => IsDefault = false;

    public void Update(
        string contactName,
        string contactPhone,
        string addressLine,
        AddressLabel? label,
        string? buildingNo,
        string? floorNo,
        string? apartmentNo,
        string? city,
        string? area,
        decimal? latitude,
        decimal? longitude)
    {
        ContactName = contactName.Trim();
        ContactPhone = contactPhone.Trim();
        AddressLine = addressLine.Trim();
        Label = label;
        BuildingNo = buildingNo?.Trim();
        FloorNo = floorNo?.Trim();
        ApartmentNo = apartmentNo?.Trim();
        City = city?.Trim();
        Area = area?.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }
}
