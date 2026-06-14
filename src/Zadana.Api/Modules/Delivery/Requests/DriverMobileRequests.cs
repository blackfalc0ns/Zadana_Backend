using System.ComponentModel.DataAnnotations;

namespace Zadana.Api.Modules.Delivery.Requests;

public record UpdateDriverPersonalProfileRequest(
    string FullName,
    string Email,
    string Phone,
    string? Address);

public record UpdateDriverVehicleProfileRequest(
    string? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    DateTime? NationalIdExpiryDate,
    DateTime? DriverLicenseExpiryDate,
    string? VehicleLicenseNumber,
    DateTime? VehicleLicenseExpiryDate,
    [Required] string? Region,
    [Required] string? City);

public record UpdateDriverDocumentsRequest(
    string? PersonalPhotoUrl,
    string? NationalIdFrontImageUrl,
    string? NationalIdBackImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl);

public record CreateDriverPayoutMethodRequest(
    [Required] string Type,
    [Required] string AccountHolderName,
    [Required] string AccountIdentifier,
    string? ProviderName,
    bool IsPrimary = false);

public record UpdateDriverPayoutMethodRequest(
    [Required] string Type,
    [Required] string AccountHolderName,
    [Required] string AccountIdentifier,
    string? ProviderName);

public record CreateDriverWithdrawalRequest(
    Guid? PaymentMethodId,
    decimal Amount);
