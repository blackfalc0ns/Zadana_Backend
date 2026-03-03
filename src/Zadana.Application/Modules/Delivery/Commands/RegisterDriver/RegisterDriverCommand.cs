using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public record RegisterDriverCommand(
    // User Info
    string FullName,
    string Email,
    string Phone,
    string Password,

    // Driver Info
    string? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    string? Address,
    string? NationalIdImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    string? PersonalPhotoUrl
) : IRequest<AuthResponseDto>;
