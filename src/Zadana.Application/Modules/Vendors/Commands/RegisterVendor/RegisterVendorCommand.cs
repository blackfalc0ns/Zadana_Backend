using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public record RegisterVendorCommand(
    // User Info
    string FullName,
    string Email,
    string Phone,
    string Password,

    // Vendor Info
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string CommercialRegistrationNumber,
    string ContactEmail,
    string ContactPhone,
    string? TaxId,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,

    // Branch Info
    string BranchName,
    string BranchAddressLine,
    decimal BranchLatitude,
    decimal BranchLongitude,
    string BranchContactPhone,
    decimal BranchDeliveryRadiusKm
) : IRequest<AuthResponseDto>;
