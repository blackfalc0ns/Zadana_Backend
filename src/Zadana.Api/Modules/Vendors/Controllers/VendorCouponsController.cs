using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.Commands.Coupons;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
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

        var couponRows = await _dbContext.Coupons
            .AsNoTracking()
            .Where(coupon => coupon.ApplicableVendors.Any(link => link.VendorId == vendorId))
            .OrderByDescending(coupon => coupon.UpdatedAtUtc)
            .Select(coupon => new
            {
                coupon.Id,
                coupon.Code,
                coupon.Title,
                DiscountType = coupon.DiscountType.ToString(),
                coupon.DiscountValue,
                coupon.MinOrderAmount,
                coupon.MaxDiscountAmount,
                coupon.StartsAtUtc,
                coupon.EndsAtUtc,
                coupon.UsageLimit,
                coupon.PerUserLimit,
                coupon.IsActive,
                coupon.CreatedAtUtc,
                coupon.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var couponIds = couponRows.Select(coupon => coupon.Id).ToArray();

        var usageCounts = couponIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await _dbContext.Orders
                .AsNoTracking()
                .Where(order => order.VendorId == vendorId && order.CouponId.HasValue && couponIds.Contains(order.CouponId.Value))
                .GroupBy(order => order.CouponId!.Value)
                .Select(group => new { CouponId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.CouponId, item => item.Count, cancellationToken);

        var coupons = couponRows
            .Select(coupon => new VendorCouponResponse(
                coupon.Id,
                coupon.Code,
                coupon.Title,
                coupon.DiscountType,
                coupon.DiscountValue,
                coupon.MinOrderAmount,
                coupon.MaxDiscountAmount,
                coupon.StartsAtUtc,
                coupon.EndsAtUtc,
                usageCounts.GetValueOrDefault(coupon.Id),
                coupon.UsageLimit,
                coupon.PerUserLimit,
                coupon.IsActive,
                coupon.CreatedAtUtc,
                coupon.UpdatedAtUtc))
            .ToList();

        return Ok(coupons);
    }

    [HttpPost]
    public async Task<ActionResult<VendorCouponResponse>> CreateCoupon(
        [FromBody] CreateVendorCouponRequest request,
        CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);

        var code = request.Code.Trim().ToUpperInvariant();
        var title = request.Title.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BadRequestException("REQUIRED_FIELD", "رمز الكوبون مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BadRequestException("REQUIRED_FIELD", "عنوان الكوبون مطلوب.");
        }

        if (request.DiscountValue <= 0)
        {
            throw new BusinessRuleException("GreaterThanZero", "قيمة الخصم لازم تكون أكبر من صفر.");
        }

        if (request.EndsAtUtc.HasValue && request.StartsAtUtc.HasValue && request.EndsAtUtc <= request.StartsAtUtc)
        {
            throw new BusinessRuleException("InvalidDateRange", "لازم يكون تاريخ الانتهاء بعد تاريخ البداية.");
        }

        if (!Enum.TryParse<CouponDiscountType>(request.DiscountType, ignoreCase: true, out var discountType))
        {
            throw new BusinessRuleException("InvalidEnum", "نوع الخصم غير صحيح.");
        }

        if (discountType == CouponDiscountType.Percentage && request.DiscountValue > 100)
        {
            throw new BusinessRuleException("PercentageTooHigh", "نسبة الخصم لازم ما تتجاوز 100.");
        }

        var duplicateExists = await _dbContext.Coupons
            .AsNoTracking()
            .AnyAsync(coupon => coupon.Code == code, cancellationToken);

        if (duplicateExists)
        {
            throw new BusinessRuleException("DUPLICATE_COUPON_CODE", "رمز الكوبون مستخدم بالفعل.");
        }

        var coupon = new Coupon(
            code,
            title,
            discountType,
            request.DiscountValue,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.UsageLimit,
            request.PerUserLimit);

        if (!request.IsActive)
        {
            coupon.UpdateStatus(false);
        }

        _dbContext.Coupons.Add(coupon);
        _dbContext.CouponVendors.Add(new CouponVendor(coupon.Id, vendorId));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new VendorCouponResponse(
            coupon.Id,
            coupon.Code,
            title,
            discountType.ToString(),
            request.DiscountValue,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            0,
            request.UsageLimit,
            request.PerUserLimit,
            coupon.IsActive,
            coupon.CreatedAtUtc,
            coupon.UpdatedAtUtc));
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
