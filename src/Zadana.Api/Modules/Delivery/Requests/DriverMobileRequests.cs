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
    string? Region,
    string? City);

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
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    decimal Amount);
