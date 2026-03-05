using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;

public record GetVendorDetailQuery(Guid VendorId) : IRequest<VendorDetailDto>;
