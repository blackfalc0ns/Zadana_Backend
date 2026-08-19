using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Primitives;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class Driver : BaseEntity
{
    public Guid UserId { get; private set; }
    public DriverVehicleType? VehicleType { get; private set; }
    public string? NationalId { get; private set; }
    /// <summary>
    /// Deterministic HMAC-SHA256 of the trimmed NationalId, kept in sync by
    /// <see cref="UpdateDetails"/> / the constructor. This column is indexed
    /// so admin search can locate a driver by national id without scanning
    /// the encrypted column (which is non-deterministic at rest).
    /// </summary>
    public string? NationalIdHash { get; private set; }
    public DateTime? NationalIdExpiryDate { get; private set; }
    public string? LicenseNumber { get; private set; }
    public DateTime? DriverLicenseExpiryDate { get; private set; }
    public string? VehicleLicenseNumber { get; private set; }
    public DateTime? VehicleLicenseExpiryDate { get; private set; }
    public string? Address { get; private set; }
    public PayoutScheduleDay PayoutDay { get; private set; } = PayoutScheduleDay.Monday;
    public string? NationalIdFrontImageUrl { get; private set; }
    public string? NationalIdBackImageUrl { get; private set; }
    public string? LicenseImageUrl { get; private set; }
    public string? VehicleImageUrl { get; private set; }
    public string? PersonalPhotoUrl { get; private set; }
    public AccountStatus Status { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool CanReceiveOrders =>
        VerificationStatus == DriverVerificationStatus.Approved &&
        Status == AccountStatus.Active &&
        HasServiceArea &&
        !HasExpiredRequiredDocuments();

    public bool CanReceiveNewOffers =>
        CanReceiveOrders &&
        IsAvailable &&
        !IsLocationUpdatesBlocked;

    public bool HasServiceArea =>
        !string.IsNullOrWhiteSpace(Region);

    public bool CanReactivate =>
        VerificationStatus == DriverVerificationStatus.Approved &&
        !HasExpiredRequiredDocuments();

    // Verification & Review
    public DriverVerificationStatus VerificationStatus { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewNote { get; private set; }


    // Geography (aligned with Vendor region/city codes from SaudiRegions/SaudiCities)
    public string? Region { get; private set; }
    public string? City { get; private set; }

    // Suspension
    public string? SuspensionReason { get; private set; }
    public bool IsLocationUpdatesBlocked { get; private set; }
    public string? LocationUpdatesBlockReason { get; private set; }
    public DateTime? LocationUpdatesBlockedAtUtc { get; private set; }
    public Guid? LocationUpdatesBlockedByUserId { get; private set; }
    public DateTime? CommitmentClearedAtUtc { get; private set; }
    public Guid? CommitmentClearedByUserId { get; private set; }
    public string? CommitmentClearNote { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;
    public ICollection<DriverLocation> Locations { get; private set; } = [];
    public ICollection<DeliveryAssignment> Assignments { get; private set; } = [];
    public ICollection<DriverNote> Notes { get; private set; } = [];
    public ICollection<DriverIncident> Incidents { get; private set; } = [];
    public ICollection<DriverDocumentReview> DocumentReviews { get; private set; } = [];

    private Driver() { }

    public Driver(
        Guid userId,
        DriverVehicleType? vehicleType,
        string? nationalId,
        string? licenseNumber,
        DateTime? nationalIdExpiryDate = null,
        DateTime? driverLicenseExpiryDate = null,
        string? vehicleLicenseNumber = null,
        DateTime? vehicleLicenseExpiryDate = null,
        string? address = null,
        string? nationalIdFrontImageUrl = null,
        string? nationalIdBackImageUrl = null,
        string? licenseImageUrl = null,
        string? vehicleImageUrl = null,
        string? personalPhotoUrl = null,
        string? region = null,
        string? city = null)
    {
        UserId = userId;
        VehicleType = vehicleType;
        NationalId = nationalId?.Trim();
        NationalIdHash = SearchableHashProvider.Compute(NationalId);
        NationalIdExpiryDate = nationalIdExpiryDate?.Date;
        LicenseNumber = licenseNumber?.Trim();
        DriverLicenseExpiryDate = driverLicenseExpiryDate?.Date;
        VehicleLicenseNumber = vehicleLicenseNumber?.Trim();
        VehicleLicenseExpiryDate = vehicleLicenseExpiryDate?.Date;
        Address = address?.Trim();
        NationalIdFrontImageUrl = nationalIdFrontImageUrl;
        NationalIdBackImageUrl = nationalIdBackImageUrl;
        LicenseImageUrl = licenseImageUrl;
        VehicleImageUrl = vehicleImageUrl;
        PersonalPhotoUrl = personalPhotoUrl;
        Region = region?.Trim().ToUpperInvariant();
        City = city?.Trim().ToUpperInvariant();
        Status = AccountStatus.Pending;
        IsAvailable = false;
        VerificationStatus = DetermineInitialVerificationStatus(nationalIdFrontImageUrl, licenseImageUrl, vehicleImageUrl, personalPhotoUrl);
    }

    public void UpdateDetails(
        DriverVehicleType? vehicleType,
        string? nationalId,
        string? licenseNumber,
        DateTime? nationalIdExpiryDate,
        DateTime? driverLicenseExpiryDate,
        string? vehicleLicenseNumber,
        DateTime? vehicleLicenseExpiryDate)
    {
        VehicleType = vehicleType;
        NationalId = nationalId?.Trim();
        NationalIdHash = SearchableHashProvider.Compute(NationalId);
        LicenseNumber = licenseNumber?.Trim();
        NationalIdExpiryDate = nationalIdExpiryDate?.Date;
        DriverLicenseExpiryDate = driverLicenseExpiryDate?.Date;
        VehicleLicenseNumber = NormalizeOptional(vehicleLicenseNumber);
        VehicleLicenseExpiryDate = vehicleLicenseExpiryDate?.Date;
    }

    public void UpdateAddress(string? address)
    {
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    public void UpdatePayoutDay(PayoutScheduleDay payoutDay)
    {
        PayoutDay = PayoutScheduleDayPolicy.EnsureAllowed(payoutDay);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateServiceArea(string? region, string? city)
    {
        Region = region?.Trim().ToUpperInvariant();
        City = city?.Trim().ToUpperInvariant();
    }

    public void UpdateDocuments(
        string? nationalIdFrontImageUrl,
        string? nationalIdBackImageUrl,
        string? licenseImageUrl,
        string? vehicleImageUrl,
        string? personalPhotoUrl)
    {
        if (!string.IsNullOrWhiteSpace(nationalIdFrontImageUrl))
            NationalIdFrontImageUrl = nationalIdFrontImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(nationalIdBackImageUrl))
            NationalIdBackImageUrl = nationalIdBackImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(licenseImageUrl))
            LicenseImageUrl = licenseImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(vehicleImageUrl))
            VehicleImageUrl = vehicleImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(personalPhotoUrl))
            PersonalPhotoUrl = personalPhotoUrl.Trim();
    }

    public DriverDocumentReview GetOrCreateDocumentReview(DriverDocumentType type)
    {
        var review = DocumentReviews.FirstOrDefault(item => item.Type == type);
        if (review is not null)
        {
            return review;
        }

        review = new DriverDocumentReview(Id, type);
        DocumentReviews.Add(review);
        return review;
    }

    public void ResetDocumentReviewToPending(DriverDocumentType type)
    {
        var review = DocumentReviews.FirstOrDefault(item => item.Type == type);
        review?.ResetToPending();
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
        if (VerificationStatus != DriverVerificationStatus.Approved || HasExpiredRequiredDocuments())
            return;

        Status = AccountStatus.Active;
        SuspensionReason = null;
    }

    public void ClearOperationalRestrictions(Guid adminUserId, string? note = null)
    {
        if (VerificationStatus != DriverVerificationStatus.Approved || HasExpiredRequiredDocuments())
            return;

        Status = AccountStatus.Active;
        SuspensionReason = null;
        UnblockLocationUpdates();
        CommitmentClearedAtUtc = DateTime.UtcNow;
        CommitmentClearedByUserId = adminUserId;
        CommitmentClearNote = NormalizeOptional(note);
        IsAvailable = false;
    }

    public void Ban(string? reason = null)
    {
        Status = AccountStatus.Banned;
        IsAvailable = false;
        SuspensionReason = NormalizeOptional(reason);
    }

    public void ToggleAvailability(bool isAvailable)
    {
        // Only approved and active drivers can go available
        if (isAvailable && !CanReceiveOrders)
            return;

        IsAvailable = isAvailable;
    }

    public void BlockLocationUpdates(Guid adminUserId, string? reason = null)
    {
        IsLocationUpdatesBlocked = true;
        IsAvailable = false;
        LocationUpdatesBlockReason = NormalizeOptional(reason);
        LocationUpdatesBlockedAtUtc = DateTime.UtcNow;
        LocationUpdatesBlockedByUserId = adminUserId;
    }

    public void UnblockLocationUpdates()
    {
        IsLocationUpdatesBlocked = false;
        LocationUpdatesBlockReason = null;
        LocationUpdatesBlockedAtUtc = null;
        LocationUpdatesBlockedByUserId = null;
    }

    public bool HasExpiredRequiredDocuments()
    {
        var today = SaudiTime.Today;
        return (NationalIdExpiryDate.HasValue && NationalIdExpiryDate.Value.Date < today)
            || (DriverLicenseExpiryDate.HasValue && DriverLicenseExpiryDate.Value.Date < today)
            || (VehicleLicenseExpiryDate.HasValue && VehicleLicenseExpiryDate.Value.Date < today);
    }

    public bool ApplyDocumentExpiryLock(string? note = null)
    {
        if (!HasExpiredRequiredDocuments())
        {
            return false;
        }

        var changed = false;
        var normalizedNote = NormalizeOptional(note) ?? "expired_required_documents";

        if (VerificationStatus != DriverVerificationStatus.NeedsDocuments)
        {
            VerificationStatus = DriverVerificationStatus.NeedsDocuments;
            changed = true;
        }

        if (IsAvailable)
        {
            IsAvailable = false;
            changed = true;
        }

        if (!string.Equals(ReviewNote, normalizedNote, StringComparison.Ordinal))
        {
            ReviewNote = normalizedNote;
            changed = true;
        }

        if (Status is not AccountStatus.Suspended and not AccountStatus.Banned)
        {
            if (Status != AccountStatus.Inactive)
            {
                Status = AccountStatus.Inactive;
                changed = true;
            }
        }

        return changed;
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
