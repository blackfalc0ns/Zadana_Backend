using System.Text.Json;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.DTOs;

public static class PendingRegistrationPayloadSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    public static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson, JsonOptions)
        ?? throw new InvalidOperationException("INVALID_PENDING_PAYLOAD");
}

public sealed record PendingCustomerPayload(
    string AddressLine,
    string? Label,
    string? BuildingNo,
    string? FloorNo,
    string? ApartmentNo,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude);

public sealed record PendingVendorPayload(
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality,
    string Region,
    string City,
    string NationalAddress,
    string? TaxId,
    string? LicenseNumber,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl,
    string BranchName,
    string BranchAddressLine,
    decimal BranchLatitude,
    decimal BranchLongitude,
    string BranchContactPhone,
    decimal BranchDeliveryRadiusKm,
    string? PayoutDay);

public sealed record PendingDriverPayload(
    DriverVehicleType? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    DateTime? NationalIdExpiryDate,
    DateTime? DriverLicenseExpiryDate,
    string? VehicleLicenseNumber,
    DateTime? VehicleLicenseExpiryDate,
    string? Address,
    string? Region,
    string? City,
    string? NationalIdFrontImageUrl,
    string? NationalIdBackImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    string? PersonalPhotoUrl);

public sealed record StartPendingRegistrationRequest(
    string FullName,
    string Email,
    string? PhoneNumber,
    string Password,
    UserRole Role,
    string PayloadJson,
    string? ProfilePhotoUrl = null);

public sealed record PendingRegistrationSnapshot(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    UserRole Role,
    string? ProfilePhotoUrl,
    Guid? ExistingUserId = null,
    string? OtpEmail = null)
{
    public string OtpDestinationEmail => string.IsNullOrWhiteSpace(OtpEmail) ? Email : OtpEmail;
}

public enum PendingRegistrationStartStatus
{
    Succeeded,
    DuplicateEmailOrPhone,
    Failed
}

public sealed record PendingRegistrationStartResult(
    PendingRegistrationStartStatus Status,
    PendingRegistrationSnapshot? Pending = null,
    string? PlainOtpCode = null,
    string? RegistrationToken = null,
    IReadOnlyCollection<string>? Errors = null);

public enum PendingOtpDispatchStatus
{
    Succeeded,
    NotFound,
    Expired,
    CooldownActive,
    Failed
}

public sealed record PendingOtpDispatchResult(
    PendingOtpDispatchStatus Status,
    PendingRegistrationSnapshot? Pending = null,
    string? PlainOtpCode = null,
    string? RegistrationToken = null,
    int? CooldownSecondsRemaining = null,
    IReadOnlyCollection<string>? Errors = null);

public enum PendingCompletionStatus
{
    Succeeded,
    NotFound,
    Expired,
    InvalidOtp,
    Failed
}

public sealed record PendingCompletionResult(
    PendingCompletionStatus Status,
    IdentityAccountSnapshot? Account = null,
    UserRole? Role = null,
    string? PayloadJson = null,
    string? RegistrationToken = null,
    IReadOnlyCollection<string>? Errors = null,
    bool LinkedExistingAccount = false,
    string? RegistrationEmail = null,
    string? RegistrationPhone = null);
