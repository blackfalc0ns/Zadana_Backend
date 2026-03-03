using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public record RegisterCustomerCommand(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? ProfilePhotoUrl,
    string? Address,
    decimal? Latitude,
    decimal? Longitude
) : IRequest<AuthResponseDto>;
