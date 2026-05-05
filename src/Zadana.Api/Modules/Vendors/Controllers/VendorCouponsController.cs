using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.Commands.Coupons;
using Zadana.Application.Modules.Marketing.Commands.CreateCoupon;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendor/coupons")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorCouponsController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentVendorService _currentVendorService;

    public VendorCouponsController(
        IApplicationDbContext dbContext,
        ICurrentVendorService currentVendorService)
    {
        _dbContext = dbContext;
        _currentVendorService = currentVendorService;
    }

    [HttpGet]
    public async Task<ActionResult<List<VendorCouponResponse>>> GetCoupons(CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);

        var coupons = await QueryVendorCoupons(vendorId)
            .OrderByDescending(coupon => coupon.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(coupons);
    }

    [HttpPost]
    public async Task<ActionResult<VendorCouponResponse>> CreateCoupon(
        [FromBody] CreateVendorCouponRequest request,
        CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);

        var coupon = await Sender.Send(new CreateCouponCommand(
            request.Code,
            request.Title,
            request.DiscountType,
            request.DiscountValue,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.UsageLimit,
            request.PerUserLimit,
            [vendorId]), cancellationToken);

        if (!request.IsActive)
        {
            await Sender.Send(new DeactivateCouponCommand(coupon.Id), cancellationToken);
        }

        var result = await QueryVendorCoupons(vendorId)
            .FirstOrDefaultAsync(item => item.Id == coupon.Id, cancellationToken)
            ?? throw new NotFoundException("Coupon", coupon.Id);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult> ActivateCoupon(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCouponBelongsToCurrentVendorAsync(id, cancellationToken);
        await Sender.Send(new ActivateCouponCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult> DeactivateCoupon(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCouponBelongsToCurrentVendorAsync(id, cancellationToken);
        await Sender.Send(new DeactivateCouponCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCoupon(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCouponBelongsToCurrentVendorAsync(id, cancellationToken);
        await Sender.Send(new DeleteCouponCommand(id), cancellationToken);
        return NoContent();
    }

    private async Task EnsureCouponBelongsToCurrentVendorAsync(Guid couponId, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var exists = await _dbContext.CouponVendors
            .AsNoTracking()
            .AnyAsync(item => item.CouponId == couponId && item.VendorId == vendorId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Coupon", couponId);
        }
    }

    private IQueryable<VendorCouponResponse> QueryVendorCoupons(Guid vendorId)
    {
        return _dbContext.Coupons
            .AsNoTracking()
            .Where(coupon => coupon.ApplicableVendors.Any(link => link.VendorId == vendorId))
            .Select(coupon => new VendorCouponResponse(
                coupon.Id,
                coupon.Code,
                coupon.Title,
                coupon.DiscountType.ToString(),
                coupon.DiscountValue,
                coupon.MinOrderAmount,
                coupon.MaxDiscountAmount,
                coupon.StartsAtUtc,
                coupon.EndsAtUtc,
                _dbContext.Orders.Count(order => order.VendorId == vendorId && order.CouponId == coupon.Id),
                coupon.UsageLimit,
                coupon.PerUserLimit,
                coupon.IsActive,
                coupon.CreatedAtUtc,
                coupon.UpdatedAtUtc));
    }
}

public record CreateVendorCouponRequest(
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
    bool IsActive);

public record VendorCouponResponse(
    Guid Id,
    string Code,
    string Title,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinOrderAmount,
    decimal? MaxDiscountAmount,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    int UsageCount,
    int? UsageLimit,
    int? PerUserLimit,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
