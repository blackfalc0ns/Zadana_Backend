using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public record RegisterCustomerCommand(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? ProfilePhotoUrl,
    string AddressLine,
    string? Label, // "Home", "Work", "Other"
    string? BuildingNo,
    string? FloorNo,
    string? ApartmentNo,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude
) : IRequest<AuthResponseDto>;
