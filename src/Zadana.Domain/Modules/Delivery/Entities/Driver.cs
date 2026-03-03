using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class Driver : BaseEntity
{
    public Guid UserId { get; private set; }
    public string? VehicleType { get; private set; }
    public string? NationalId { get; private set; }
    public string? LicenseNumber { get; private set; }
    public string? Address { get; private set; }
    public string? NationalIdImageUrl { get; private set; }
    public string? LicenseImageUrl { get; private set; }
    public string? VehicleImageUrl { get; private set; }
    public string? PersonalPhotoUrl { get; private set; }
    public AccountStatus Status { get; private set; }
    public bool IsAvailable { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;
    public ICollection<DriverLocation> Locations { get; private set; } = [];
    public ICollection<DeliveryAssignment> Assignments { get; private set; } = [];

    private Driver() { }

    public Driver(
        Guid userId,
        string? vehicleType,
        string? nationalId,
        string? licenseNumber,
        string? address = null,
        string? nationalIdImageUrl = null,
        string? licenseImageUrl = null,
        string? vehicleImageUrl = null,
        string? personalPhotoUrl = null)
    {
        UserId = userId;
        VehicleType = vehicleType?.Trim();
        NationalId = nationalId?.Trim();
        LicenseNumber = licenseNumber?.Trim();
        Address = address?.Trim();
        NationalIdImageUrl = nationalIdImageUrl;
        LicenseImageUrl = licenseImageUrl;
        VehicleImageUrl = vehicleImageUrl;
        PersonalPhotoUrl = personalPhotoUrl;
        Status = AccountStatus.Pending;
        IsAvailable = false;
    }

    public void UpdateDetails(string? vehicleType, string? nationalId, string? licenseNumber)
    {
        VehicleType = vehicleType?.Trim();
        NationalId = nationalId?.Trim();
        LicenseNumber = licenseNumber?.Trim();
    }

    public void Approve() => Status = AccountStatus.Active;
    public void Suspend() => Status = AccountStatus.Suspended;
    public void Ban() => Status = AccountStatus.Banned;

    public void ToggleAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
    }
}
