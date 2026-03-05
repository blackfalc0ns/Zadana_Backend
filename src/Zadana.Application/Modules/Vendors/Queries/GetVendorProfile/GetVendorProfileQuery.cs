using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

public record GetVendorProfileQuery : IRequest<VendorProfileDto>;
