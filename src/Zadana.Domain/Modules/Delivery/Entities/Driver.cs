using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class Driver : BaseEntity
{
    public Guid UserId { get; private set; }
    public DriverVehicleType? VehicleType { get; private set; }
    public string? NationalId { get; private set; }
    public string? LicenseNumber { get; private set; }
    public string? Address { get; private set; }
    public string? NationalIdImageUrl { get; private set; }
    public string? LicenseImageUrl { get; private set; }
    public string? VehicleImageUrl { get; private set; }
    public string? PersonalPhotoUrl { get; private set; }
    public AccountStatus Status { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool CanReceiveOrders => VerificationStatus == DriverVerificationStatus.Approved && Status == AccountStatus.Active;

    // Verification & Review
    public DriverVerificationStatus VerificationStatus { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewNote { get; private set; }

    // Zone
    public Guid? PrimaryZoneId { get; private set; }

    // Geography (aligned with Vendor region/city codes from SaudiRegions/SaudiCities)
    public string? Region { get; private set; }
    public string? City { get; private set; }

    // Suspension
    public string? SuspensionReason { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;
    public DeliveryZone? PrimaryZone { get; private set; }
    public ICollection<DriverLocation> Locations { get; private set; } = [];
    public ICollection<DeliveryAssignment> Assignments { get; private set; } = [];
    public ICollection<DriverNote> Notes { get; private set; } = [];
    public ICollection<DriverIncident> Incidents { get; private set; } = [];

    private Driver() { }

    public Driver(
        Guid userId,
        DriverVehicleType? vehicleType,
        string? nationalId,
        string? licenseNumber,
        string? address = null,
        string? nationalIdImageUrl = null,
        string? licenseImageUrl = null,
        string? vehicleImageUrl = null,
        string? personalPhotoUrl = null,
        string? region = null,
        string? city = null)
    {
        UserId = userId;
        VehicleType = vehicleType;
        NationalId = nationalId?.Trim();
        LicenseNumber = licenseNumber?.Trim();
        Address = address?.Trim();
        NationalIdImageUrl = nationalIdImageUrl;
        LicenseImageUrl = licenseImageUrl;
        VehicleImageUrl = vehicleImageUrl;
        PersonalPhotoUrl = personalPhotoUrl;
        Region = region?.Trim().ToUpperInvariant();
        City = city?.Trim().ToUpperInvariant();
        Status = AccountStatus.Pending;
        IsAvailable = false;
        VerificationStatus = DetermineInitialVerificationStatus(nationalIdImageUrl, licenseImageUrl, vehicleImageUrl, personalPhotoUrl);
    }

    public void UpdateDetails(DriverVehicleType? vehicleType, string? nationalId, string? licenseNumber)
    {
        VehicleType = vehicleType;
        NationalId = nationalId?.Trim();
        LicenseNumber = licenseNumber?.Trim();
    }

    public void UpdateAddress(string? address)
    {
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    public void UpdateServiceArea(string? region, string? city)
    {
        Region = region?.Trim().ToUpperInvariant();
        City = city?.Trim().ToUpperInvariant();
    }

    public void UpdateDocuments(
        string? nationalIdImageUrl,
        string? licenseImageUrl,
        string? vehicleImageUrl,
        string? personalPhotoUrl)
    {
        NationalIdImageUrl = NormalizeOptional(nationalIdImageUrl);
        LicenseImageUrl = NormalizeOptional(licenseImageUrl);
        VehicleImageUrl = NormalizeOptional(vehicleImageUrl);
        PersonalPhotoUrl = NormalizeOptional(personalPhotoUrl);
    }

    public void RefreshProfileReviewState(bool hasRequiredProfileData, bool sensitiveChange, string? note = null)
    {
        if (!sensitiveChange && VerificationStatus == DriverVerificationStatus.Approved)
        {
            return;
        }

        VerificationStatus = hasRequiredProfileData
            ? DriverVerificationStatus.UnderReview
            : DriverVerificationStatus.NeedsDocuments;

        ReviewNote = NormalizeOptional(note);
        ReviewedAtUtc = null;
        ReviewedByUserId = null;
        IsAvailable = false;

        if (Status == AccountStatus.Inactive && VerificationStatus != DriverVerificationStatus.Rejected)
        {
            Status = AccountStatus.Pending;
        }
    }

    public void Approve(Guid reviewerUserId, string? note = null)
    {
        VerificationStatus = DriverVerificationStatus.Approved;
        Status = AccountStatus.Active;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewerUserId;
        ReviewNote = note?.Trim();
        SuspensionReason = null;
    }

    public void RequestDocuments(Guid reviewerUserId, string? note = null)
    {
        VerificationStatus = DriverVerificationStatus.NeedsDocuments;
        IsAvailable = false;

        // Active drivers should go back to Pending while docs are missing
        if (Status == AccountStatus.Active)
        {
            Status = AccountStatus.Pending;
        }

        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewerUserId;
        ReviewNote = note?.Trim();
    }

    public void Reject(Guid reviewerUserId, string? note = null)
    {
        VerificationStatus = DriverVerificationStatus.Rejected;
        Status = AccountStatus.Inactive;
        IsAvailable = false;
        SuspensionReason = null;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = reviewerUserId;
        ReviewNote = note?.Trim();
    }

    public void Suspend(string? reason = null)
    {
        Status = AccountStatus.Suspended;
        IsAvailable = false;
        SuspensionReason = reason?.Trim();
    }

    public void Reactivate()
    {
        if (VerificationStatus != DriverVerificationStatus.Approved)
            return;

        Status = AccountStatus.Active;
        SuspensionReason = null;
    }

    public void Ban() => Status = AccountStatus.Banned;

    public void ToggleAvailability(bool isAvailable)
    {
        // Only approved and active drivers can go available
        if (isAvailable && !CanReceiveOrders)
            return;

        IsAvailable = isAvailable;
    }

    public void AssignZone(Guid zoneId, DeliveryZone? zone = null)
    {
        PrimaryZoneId = zoneId;
        PrimaryZone = zone;
    }

    public void ClearZone()
    {
        PrimaryZoneId = null;
        PrimaryZone = null;
    }

    private static DriverVerificationStatus DetermineInitialVerificationStatus(
        string? nationalIdImageUrl, string? licenseImageUrl, string? vehicleImageUrl, string? personalPhotoUrl)
    {
        var hasAllDocs = !string.IsNullOrWhiteSpace(nationalIdImageUrl)
            && !string.IsNullOrWhiteSpace(licenseImageUrl)
            && !string.IsNullOrWhiteSpace(vehicleImageUrl)
            && !string.IsNullOrWhiteSpace(personalPhotoUrl);

        return hasAllDocs ? DriverVerificationStatus.UnderReview : DriverVerificationStatus.NeedsDocuments;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
