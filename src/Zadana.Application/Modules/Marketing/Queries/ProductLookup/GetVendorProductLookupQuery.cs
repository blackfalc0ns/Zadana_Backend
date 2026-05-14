using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.Application.Modules.Marketing.Queries.ProductLookup;

public record GetVendorProductLookupQuery(string? Search) : IRequest<List<VendorProductLookupDto>>;

public class GetVendorProductLookupQueryHandler : IRequestHandler<GetVendorProductLookupQuery, List<VendorProductLookupDto>>
{
    private readonly IApplicationDbContext _context;
    public GetVendorProductLookupQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<VendorProductLookupDto>> Handle(GetVendorProductLookupQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VendorProducts
            .AsNoTracking()
            .Include(vp => vp.MasterProduct)
            .Include(vp => vp.Vendor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(vp =>
                vp.MasterProduct.NameAr.ToLower().Contains(search) ||
                vp.MasterProduct.NameEn.ToLower().Contains(search) ||
                (vp.CustomNameAr != null && vp.CustomNameAr.ToLower().Contains(search)) ||
                (vp.CustomNameEn != null && vp.CustomNameEn.ToLower().Contains(search)) ||
                vp.Vendor.BusinessNameAr.ToLower().Contains(search) ||
                vp.Vendor.BusinessNameEn.ToLower().Contains(search));
        }

        return await query
            .OrderBy(vp => vp.MasterProduct.NameAr)
            .Take(50)
            .Select(vp => new VendorProductLookupDto(
                vp.Id,
                !string.IsNullOrWhiteSpace(vp.CustomNameAr) ? vp.CustomNameAr : vp.MasterProduct.NameAr,
                !string.IsNullOrWhiteSpace(vp.CustomNameEn) ? vp.CustomNameEn : vp.MasterProduct.NameEn,
                vp.Vendor.BusinessNameAr,
                vp.Vendor.BusinessNameEn))
            .ToListAsync(cancellationToken);
    }
}
