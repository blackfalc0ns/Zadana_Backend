namespace Zadana.Api.Modules.Delivery.Requests;

public record RegisterDriverRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    string? Address,
    string? NationalIdImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    string? PersonalPhotoUrl);
