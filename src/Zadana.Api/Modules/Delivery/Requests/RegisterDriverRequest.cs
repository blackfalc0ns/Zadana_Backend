using System.ComponentModel.DataAnnotations;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Api.Modules.Delivery.Requests;

public record RegisterDriverRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    DateTime? NationalIdExpiryDate,
    DateTime? DriverLicenseExpiryDate,
    string? VehicleLicenseNumber,
    DateTime? VehicleLicenseExpiryDate,
    string? Address,
    [Required] string? Region,
    [Required] string? City,
    string? NationalIdFrontImageUrl,
    string? NationalIdBackImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    string? PersonalPhotoUrl);
