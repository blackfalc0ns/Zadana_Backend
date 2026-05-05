using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.Commands.Coupons;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.Application.Modules.Marketing.Queries.Coupons;

public record GetCouponsQuery() : IRequest<List<CouponAdminDto>>;

public record GetCouponByIdQuery(Guid Id) : IRequest<CouponAdminDto>;

public class GetCouponsQueryHandler : IRequestHandler<GetCouponsQuery, List<CouponAdminDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCouponsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CouponAdminDto>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Coupons
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new CouponAdminDto(
                x.Id,
                x.Code,
                x.Title,
                x.DiscountType.ToString(),
                x.DiscountValue,
                x.MinOrderAmount,
                x.MaxDiscountAmount,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.UsageLimit,
                x.PerUserLimit,
                x.IsActive,
                x.ApplicableVendors.Count,
                x.ApplicableVendors
                    .Select(v => new CouponVendorAdminDto(
                        v.VendorId,
                        v.Vendor.BusinessNameAr,
                        v.Vendor.BusinessNameEn))
                    .OrderBy(v => v.VendorNameAr)
                    .ToList(),
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

public class GetCouponByIdQueryHandler : IRequestHandler<GetCouponByIdQuery, CouponAdminDto>
{
    private readonly IApplicationDbContext _context;

    public GetCouponByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<CouponAdminDto> Handle(GetCouponByIdQuery request, CancellationToken cancellationToken)
    {
        return MarketingCouponMappings.ToCouponDto(_context, request.Id, cancellationToken);
    }
}
