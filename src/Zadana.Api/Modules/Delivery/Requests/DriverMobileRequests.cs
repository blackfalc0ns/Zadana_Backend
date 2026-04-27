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
    Guid? PrimaryZoneId,
    string? Region,
    string? City);

public record UpdateDriverDocumentsRequest(
    string? PersonalPhotoUrl,
    string? NationalIdImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl);

public record CreateDriverPayoutMethodRequest(
    string Type,
    string AccountHolderName,
    string AccountIdentifier,
    string? ProviderName,
    bool IsPrimary = false);

public record UpdateDriverPayoutMethodRequest(
    string Type,
    string AccountHolderName,
    string AccountIdentifier,
    string? ProviderName);

public record CreateDriverWithdrawalRequest(
    Guid? PaymentMethodId,
    decimal Amount);
