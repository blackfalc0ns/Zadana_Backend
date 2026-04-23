using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public record RegisterDriverCommand(
    // User Info
    string FullName,
    string Email,
    string Phone,
    string Password,

    // Driver Info
    DriverVehicleType? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    string? Address,
    string? NationalIdImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    string? PersonalPhotoUrl
) : IRequest<AuthResponseDto>;
