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
        var coupons = await _context.Coupons
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new CouponSummaryProjection(
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
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        if (coupons.Count == 0)
        {
            return [];
        }

        var couponIds = coupons.Select(x => x.Id).ToArray();
        var vendorsByCouponId = await _context.CouponVendors
            .AsNoTracking()
            .Where(x => couponIds.Contains(x.CouponId))
            .Select(x => new
            {
                x.CouponId,
                Vendor = new CouponVendorAdminDto(
                    x.VendorId,
                    x.Vendor.BusinessNameAr,
                    x.Vendor.BusinessNameEn)
            })
            .ToListAsync(cancellationToken);

        var vendorLookup = vendorsByCouponId
            .GroupBy(x => x.CouponId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CouponVendorAdminDto>)group
                    .Select(x => x.Vendor)
                    .OrderBy(x => x.VendorNameAr)
                    .ThenBy(x => x.VendorNameEn)
                    .ToList());

        return coupons
            .Select(x => new CouponAdminDto(
                x.Id,
                x.Code,
                x.Title,
                x.DiscountType,
                x.DiscountValue,
                x.MinOrderAmount,
                x.MaxDiscountAmount,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.UsageLimit,
                x.PerUserLimit,
                x.IsActive,
                x.AssignedVendorsCount,
                vendorLookup.GetValueOrDefault(x.Id, Array.Empty<CouponVendorAdminDto>()),
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
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

internal sealed record CouponSummaryProjection(
    Guid Id,
    string Code,
    string Title,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinOrderAmount,
    decimal? MaxDiscountAmount,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    int? UsageLimit,
    int? PerUserLimit,
    bool IsActive,
    int AssignedVendorsCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
