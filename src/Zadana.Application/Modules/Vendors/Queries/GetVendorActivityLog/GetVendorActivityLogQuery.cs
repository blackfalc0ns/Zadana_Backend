using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorActivityLog;

public record GetVendorActivityLogQuery(
    Guid VendorId,
    string? Type,
    string? Severity,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Page,
    int PageSize) : IRequest<VendorActivityLogPageDto>;
