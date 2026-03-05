using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;

public record UpdateVendorProfileCommand(
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string ContactEmail,
    string ContactPhone,
    string? TaxId) : IRequest<VendorProfileDto>;
