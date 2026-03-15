using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;

namespace Zadana.Application.Modules.Vendors.Queries.GetAllVendors;

public class GetAllVendorsQueryHandler : IRequestHandler<GetAllVendorsQuery, PaginatedList<VendorListItemDto>>
{
    private readonly IApplicationDbContext _db;

    public GetAllVendorsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedList<VendorListItemDto>> Handle(GetAllVendorsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Vendors
            .Include(v => v.User)
            .AsNoTracking()
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(v => v.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(v =>
                v.BusinessNameAr.ToLower().Contains(search) ||
                v.BusinessNameEn.ToLower().Contains(search) ||
                v.ContactPhone.Contains(search) ||
                v.ContactEmail.ToLower().Contains(search) ||
                v.User.FullName.ToLower().Contains(search));
        }

        query = query.OrderByDescending(v => v.CreatedAtUtc);

        var projected = query.Select(v => new VendorListItemDto(
            v.Id,
            v.BusinessNameAr,
            v.BusinessNameEn,
            v.BusinessType,
            v.Status.ToString(),
            v.User.FullName,
            v.ContactPhone,
            v.CreatedAtUtc,
            v.ContactEmail,
            v.CommissionRate));

        return await PaginatedList<VendorListItemDto>.CreateAsync(projected, request.Page, request.PageSize, cancellationToken);
    }
}
