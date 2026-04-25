using Zadana.Domain.Modules.Delivery.Entities;

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
            string.IsNullOrWhiteSpace(driver.NationalId))
        {
            missing.Add("missing_vehicle_info");
        }

        if (string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) ||
            string.IsNullOrWhiteSpace(driver.NationalIdImageUrl) ||
            string.IsNullOrWhiteSpace(driver.LicenseImageUrl) ||
            string.IsNullOrWhiteSpace(driver.VehicleImageUrl))
        {
            missing.Add("missing_documents");
        }

        if (!driver.PrimaryZoneId.HasValue)
        {
            missing.Add("missing_zone_selection");
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

        var checklist = new[]
        {
            new DriverHomeChecklistItemDto(
                "personal_info",
                !missingRequirements.Contains("missing_personal_info"),
                missingRequirements.Contains("missing_personal_info") ? "missing_personal_info_note" : null,
                false),
            new DriverHomeChecklistItemDto(
                "vehicle_info",
                !missingRequirements.Contains("missing_vehicle_info"),
                missingRequirements.Contains("missing_vehicle_info") ? "missing_vehicle_info_note" : null,
                true),
            new DriverHomeChecklistItemDto(
                "national_id_document",
                !string.IsNullOrWhiteSpace(driver.NationalIdImageUrl),
                string.IsNullOrWhiteSpace(driver.NationalIdImageUrl) ? "missing_document_note" : null,
                true),
            new DriverHomeChecklistItemDto(
                "license_document",
                !string.IsNullOrWhiteSpace(driver.LicenseImageUrl),
                string.IsNullOrWhiteSpace(driver.LicenseImageUrl) ? "missing_document_note" : null,
                true),
            new DriverHomeChecklistItemDto(
                "vehicle_document",
                !string.IsNullOrWhiteSpace(driver.VehicleImageUrl),
                string.IsNullOrWhiteSpace(driver.VehicleImageUrl) ? "missing_document_note" : null,
                true),
            new DriverHomeChecklistItemDto(
                "personal_photo",
                !string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl),
                string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) ? "missing_document_note" : null,
                true),
            new DriverHomeChecklistItemDto(
                "zone_selection",
                !missingRequirements.Contains("missing_zone_selection"),
                missingRequirements.Contains("missing_zone_selection") ? "missing_zone_selection_note" : null,
                false)
        };

        return new DriverHomeProfileReadinessDto(
            missingRequirements.Count == 0,
            completionPercent,
            missingRequirements,
            missingRequirements.Count == 0,
            checklist);
    }
}
