using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Modules.Delivery.DTOs;

public static class DriverProfileReadinessFactory
{
    public static IReadOnlyList<string> GetMissingRequirements(Driver driver, Domain.Modules.Identity.Entities.User user)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(user.FullName) ||
            string.IsNullOrWhiteSpace(user.Email) ||
            string.IsNullOrWhiteSpace(user.PhoneNumber) ||
            string.IsNullOrWhiteSpace(driver.Address))
        {
            missing.Add("missing_personal_info");
        }

        if (driver.VehicleType is null ||
            string.IsNullOrWhiteSpace(driver.LicenseNumber) ||
            string.IsNullOrWhiteSpace(driver.NationalId) ||
            string.IsNullOrWhiteSpace(driver.VehicleLicenseNumber))
        {
            missing.Add("missing_vehicle_info");
        }

        if (!HasNationalIdPacket(driver) ||
            !HasDriverLicensePacket(driver) ||
            !HasVehicleLicensePacket(driver))
        {
            missing.Add("missing_documents");
        }

        if (HasExpiredRequiredDocuments(driver))
        {
            missing.Add("expired_documents");
        }

        if (HasRejectedRequiredDocuments(driver))
        {
            missing.Add("rejected_documents");
        }

        if (string.IsNullOrWhiteSpace(driver.Region) || string.IsNullOrWhiteSpace(driver.City))
        {
            missing.Add("missing_region_city");
        }

        return missing;
    }

    public static int GetCompletionPercent(int missingCount) =>
        missingCount switch
        {
            <= 0 => 100,
            1 => 75,
            2 => 50,
            3 => 25,
            _ => 0
        };

    public static DriverHomeProfileReadinessDto BuildHomeReadiness(
        Driver driver,
        Domain.Modules.Identity.Entities.User user)
    {
        var missingRequirements = GetMissingRequirements(driver, user);
        var completionPercent = GetCompletionPercent(missingRequirements.Count);

        return new DriverHomeProfileReadinessDto(
            missingRequirements.Count == 0,
            completionPercent,
            missingRequirements,
            missingRequirements.Count == 0,
            BuildHomeChecklist(driver, missingRequirements));
    }

    public static AdminDriverProfileReadinessDto BuildAdminReadiness(
        Driver driver,
        Domain.Modules.Identity.Entities.User user)
    {
        var missingRequirements = GetMissingRequirements(driver, user);
        var completionPercent = GetCompletionPercent(missingRequirements.Count);

        return new AdminDriverProfileReadinessDto(
            missingRequirements.Count == 0,
            completionPercent,
            missingRequirements,
            missingRequirements.Count == 0,
            BuildAdminChecklist(driver, missingRequirements));
    }

    private static DriverHomeChecklistItemDto[] BuildHomeChecklist(
        Driver driver,
        IReadOnlyCollection<string> missingRequirements) =>
        BuildChecklist(
            driver,
            missingRequirements,
            (code, completed, note, critical) => new DriverHomeChecklistItemDto(code, completed, note, critical));

    private static AdminDriverVerificationChecklistItemDto[] BuildAdminChecklist(
        Driver driver,
        IReadOnlyCollection<string> missingRequirements) =>
        BuildChecklist(
            driver,
            missingRequirements,
            (code, completed, note, critical) => new AdminDriverVerificationChecklistItemDto(code, completed, note, critical));

    private static T[] BuildChecklist<T>(
        Driver driver,
        IReadOnlyCollection<string> missingRequirements,
        Func<string, bool, string?, bool, T> createItem) =>
        [
            createItem(
                "personal_info",
                !missingRequirements.Contains("missing_personal_info"),
                missingRequirements.Contains("missing_personal_info") ? "missing_personal_info_note" : null,
                false),
            createItem(
                "vehicle_info",
                !missingRequirements.Contains("missing_vehicle_info"),
                missingRequirements.Contains("missing_vehicle_info") ? "missing_vehicle_info_note" : null,
                true),
            createItem(
                "national_id_document",
                IsNationalIdReady(driver),
                ResolveDocumentChecklistNote(driver, DriverDocumentType.NationalId),
                true),
            createItem(
                "license_document",
                IsDriverLicenseReady(driver),
                ResolveDocumentChecklistNote(driver, DriverDocumentType.DriverLicense),
                true),
            createItem(
                "vehicle_document",
                IsVehicleLicenseReady(driver),
                ResolveDocumentChecklistNote(driver, DriverDocumentType.VehicleLicense),
                true),
            createItem(
                "personal_photo",
                !string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl),
                string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) ? "missing_document_note" : null,
                true),
            createItem(
                "region_city_selection",
                !missingRequirements.Contains("missing_region_city"),
                missingRequirements.Contains("missing_region_city") ? "missing_region_city_note" : null,
                false)
        ];

    public static bool HasNationalIdPacket(Driver driver) =>
        !string.IsNullOrWhiteSpace(driver.NationalIdFrontImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.NationalIdBackImageUrl) &&
        driver.NationalIdExpiryDate.HasValue;

    public static bool HasDriverLicensePacket(Driver driver) =>
        !string.IsNullOrWhiteSpace(driver.LicenseImageUrl) &&
        driver.DriverLicenseExpiryDate.HasValue;

    public static bool HasVehicleLicensePacket(Driver driver) =>
        !string.IsNullOrWhiteSpace(driver.VehicleImageUrl) &&
        driver.VehicleLicenseExpiryDate.HasValue;

    public static bool HasExpiredRequiredDocuments(Driver driver)
        => driver.HasExpiredRequiredDocuments();

    public static bool HasRejectedRequiredDocuments(Driver driver) =>
        GetReviewDecision(driver, DriverDocumentType.NationalId) == DriverDocumentReviewDecision.Rejected
        || GetReviewDecision(driver, DriverDocumentType.DriverLicense) == DriverDocumentReviewDecision.Rejected
        || GetReviewDecision(driver, DriverDocumentType.VehicleLicense) == DriverDocumentReviewDecision.Rejected;

    public static bool AreRequiredDocumentsApproved(Driver driver) =>
        IsReviewApproved(driver, DriverDocumentType.NationalId)
        && IsReviewApproved(driver, DriverDocumentType.DriverLicense)
        && IsReviewApproved(driver, DriverDocumentType.VehicleLicense);

    public static bool IsNationalIdReady(Driver driver) =>
        HasNationalIdPacket(driver)
        && !IsExpired(driver.NationalIdExpiryDate)
        && IsReviewApproved(driver, DriverDocumentType.NationalId);

    public static bool IsDriverLicenseReady(Driver driver) =>
        HasDriverLicensePacket(driver)
        && !IsExpired(driver.DriverLicenseExpiryDate)
        && IsReviewApproved(driver, DriverDocumentType.DriverLicense);

    public static bool IsVehicleLicenseReady(Driver driver) =>
        HasVehicleLicensePacket(driver)
        && !IsExpired(driver.VehicleLicenseExpiryDate)
        && IsReviewApproved(driver, DriverDocumentType.VehicleLicense);

    public static DriverDocumentReviewDecision? GetReviewDecision(Driver driver, DriverDocumentType type) =>
        driver.DocumentReviews.FirstOrDefault(item => item.Type == type)?.Decision;

    public static string? ResolveDocumentChecklistNote(Driver driver, DriverDocumentType type)
    {
        if (!HasPacket(driver, type))
        {
            return "missing_document_note";
        }

        if (IsExpired(GetExpiryDate(driver, type)))
        {
            return "expired_document_note";
        }

        return GetReviewDecision(driver, type) switch
        {
            DriverDocumentReviewDecision.Rejected => "rejected_document_note",
            DriverDocumentReviewDecision.Pending or null => "pending_document_review_note",
            _ => null
        };
    }

    private static bool IsReviewApproved(Driver driver, DriverDocumentType type) =>
        GetReviewDecision(driver, type) == DriverDocumentReviewDecision.Approved;

    private static bool HasPacket(Driver driver, DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => HasNationalIdPacket(driver),
            DriverDocumentType.DriverLicense => HasDriverLicensePacket(driver),
            DriverDocumentType.VehicleLicense => HasVehicleLicensePacket(driver),
            _ => false
        };

    private static DateTime? GetExpiryDate(Driver driver, DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => driver.NationalIdExpiryDate,
            DriverDocumentType.DriverLicense => driver.DriverLicenseExpiryDate,
            DriverDocumentType.VehicleLicense => driver.VehicleLicenseExpiryDate,
            _ => null
        };

    private static bool IsExpired(DateTime? value) =>
        value.HasValue && value.Value.Date < DateTime.UtcNow.Date;
}
