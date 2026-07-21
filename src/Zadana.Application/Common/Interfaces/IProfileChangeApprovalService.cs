using System.Text.Json.Serialization;

namespace Zadana.Application.Common.Interfaces;

public interface IProfileChangeApprovalService
{
    Task<Guid> SubmitAsync(
        Guid requestedByUserId,
        Guid targetUserId,
        string action,
        string summary,
        object payload,
        ProfileChangeApprovalAlert alert,
        CancellationToken cancellationToken = default);
}

public sealed record ProfileChangeApprovalAlert(
    string Type,
    string Category,
    string Priority,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    Guid ReferenceId,
    string TargetUrl,
    object? Data = null);

public static class ProfileChangeApprovalActions
{
    public const string VendorProfileBasic = "vendor.profile.basic";
    public const string VendorProfileStore = "vendor.profile.store";
    public const string VendorProfileOwner = "vendor.profile.owner";
    public const string VendorProfileLegal = "vendor.profile.legal";
    public const string VendorProfileBanking = "vendor.profile.banking";

    public const string DriverProfilePersonal = "driver.profile.personal";
    public const string DriverProfileVehicle = "driver.profile.vehicle";
    public const string DriverProfileDocuments = "driver.profile.documents";
    public const string DriverPayoutMethodCreate = "driver.payout_method.create";
    public const string DriverPayoutMethodUpdate = "driver.payout_method.update";
    public const string DriverPayoutMethodMakePrimary = "driver.payout_method.make_primary";
    public const string DriverPayoutMethodDelete = "driver.payout_method.delete";

    public static bool IsProfileChange(string action) =>
        action.StartsWith("vendor.profile.", StringComparison.Ordinal) ||
        action.StartsWith("driver.profile.", StringComparison.Ordinal) ||
        action.StartsWith("driver.payout_method.", StringComparison.Ordinal);
}

public sealed record VendorBasicProfileChangePayload(
    [property: JsonPropertyName("vendorId")] Guid VendorId,
    [property: JsonPropertyName("businessNameAr")] string BusinessNameAr,
    [property: JsonPropertyName("businessNameEn")] string BusinessNameEn,
    [property: JsonPropertyName("businessType")] string BusinessType,
    [property: JsonPropertyName("contactEmail")] string ContactEmail,
    [property: JsonPropertyName("contactPhone")] string ContactPhone,
    [property: JsonPropertyName("taxId")] string? TaxId);

public sealed record VendorStoreProfileChangePayload(
    [property: JsonPropertyName("vendorId")] Guid VendorId,
    [property: JsonPropertyName("commercialRegisterDocumentUrl")] string? CommercialRegisterDocumentUrl,
    [property: JsonPropertyName("commercialRegistrationNumber")] string? CommercialRegistrationNumber);

public sealed record VendorOwnerProfileChangePayload(
    [property: JsonPropertyName("vendorId")] Guid VendorId,
    [property: JsonPropertyName("ownerName")] string OwnerName,
    [property: JsonPropertyName("ownerEmail")] string OwnerEmail,
    [property: JsonPropertyName("ownerPhone")] string OwnerPhone,
    [property: JsonPropertyName("idNumber")] string? IdNumber,
    [property: JsonPropertyName("nationality")] string? Nationality);

public sealed record VendorLegalProfileChangePayload(
    [property: JsonPropertyName("vendorId")] Guid VendorId,
    [property: JsonPropertyName("commercialRegistrationNumber")] string CommercialRegistrationNumber,
    [property: JsonPropertyName("commercialRegistrationExpiryDate")] DateTime? CommercialRegistrationExpiryDate,
    [property: JsonPropertyName("taxId")] string? TaxId,
    [property: JsonPropertyName("licenseNumber")] string? LicenseNumber,
    [property: JsonPropertyName("commercialRegisterDocumentUrl")] string? CommercialRegisterDocumentUrl,
    [property: JsonPropertyName("taxDocumentUrl")] string? TaxDocumentUrl,
    [property: JsonPropertyName("licenseDocumentUrl")] string? LicenseDocumentUrl);

public sealed record VendorBankingProfileChangePayload(
    [property: JsonPropertyName("vendorId")] Guid VendorId,
    [property: JsonPropertyName("bankName")] string BankName,
    [property: JsonPropertyName("accountHolderName")] string AccountHolderName,
    [property: JsonPropertyName("iban")] string Iban,
    [property: JsonPropertyName("swiftCode")] string? SwiftCode,
    [property: JsonPropertyName("payoutCycle")] string? PayoutCycle,
    [property: JsonPropertyName("payoutDay")] string? PayoutDay = null);

public sealed record DriverPersonalProfileChangePayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("address")] string? Address);

public sealed record DriverVehicleProfileChangePayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("vehicleType")] string? VehicleType,
    [property: JsonPropertyName("nationalId")] string? NationalId,
    [property: JsonPropertyName("licenseNumber")] string? LicenseNumber,
    [property: JsonPropertyName("nationalIdExpiryDate")] DateTime? NationalIdExpiryDate,
    [property: JsonPropertyName("driverLicenseExpiryDate")] DateTime? DriverLicenseExpiryDate,
    [property: JsonPropertyName("vehicleLicenseNumber")] string? VehicleLicenseNumber,
    [property: JsonPropertyName("vehicleLicenseExpiryDate")] DateTime? VehicleLicenseExpiryDate,
    [property: JsonPropertyName("region")] string? Region,
    [property: JsonPropertyName("city")] string? City);

public sealed record DriverDocumentsProfileChangePayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("nationalIdFrontImageUrl")] string? NationalIdFrontImageUrl,
    [property: JsonPropertyName("nationalIdBackImageUrl")] string? NationalIdBackImageUrl,
    [property: JsonPropertyName("licenseImageUrl")] string? LicenseImageUrl,
    [property: JsonPropertyName("vehicleImageUrl")] string? VehicleImageUrl,
    [property: JsonPropertyName("personalPhotoUrl")] string? PersonalPhotoUrl);

public sealed record DriverPayoutMethodCreatePayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("accountHolderName")] string AccountHolderName,
    [property: JsonPropertyName("accountIdentifier")] string AccountIdentifier,
    [property: JsonPropertyName("providerName")] string? ProviderName,
    [property: JsonPropertyName("isPrimary")] bool IsPrimary);

public sealed record DriverPayoutMethodUpdatePayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("payoutMethodId")] Guid PayoutMethodId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("accountHolderName")] string AccountHolderName,
    [property: JsonPropertyName("accountIdentifier")] string AccountIdentifier,
    [property: JsonPropertyName("providerName")] string? ProviderName);

public sealed record DriverPayoutMethodMakePrimaryPayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("payoutMethodId")] Guid PayoutMethodId);

public sealed record DriverPayoutMethodDeletePayload(
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("payoutMethodId")] Guid PayoutMethodId);
