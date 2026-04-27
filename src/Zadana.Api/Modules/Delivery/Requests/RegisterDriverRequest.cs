using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Api.Modules.Delivery.Requests;

public record RegisterDriverRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    DriverVehicleType? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    string? Address,
    Guid PrimaryZoneId,
    string? Region,
    string? City,
    string? NationalIdFrontImageUrl,
    string? NationalIdBackImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    string? PersonalPhotoUrl);
