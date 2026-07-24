using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.VendorGoogleAuth;

public record VendorGoogleAuthCommand(string IdToken) : IRequest<VendorGoogleAuthResultDto>;

public record VendorGoogleAuthResultDto(
    string Mode,
    AuthResponseDto? Auth = null,
    VendorGoogleProfileDto? Profile = null);

public record VendorGoogleProfileDto(
    string Email,
    string FullName,
    string? GivenName,
    string? FamilyName,
    string Subject);
