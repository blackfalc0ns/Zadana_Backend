using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Commands.Coupons;

public record UpdateCouponCommand(
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
    IReadOnlyCollection<Guid>? VendorIds) : IRequest<CouponAdminDto>;

public record ActivateCouponCommand(Guid Id) : IRequest;
public record DeactivateCouponCommand(Guid Id) : IRequest;
public record DeleteCouponCommand(Guid Id) : IRequest;

public class UpdateCouponCommandValidator : AbstractValidator<UpdateCouponCommand>
{
    public UpdateCouponCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        var couponIdName = localizer["CouponIdField"];
        var couponCodeName = localizer["CouponCodeField"];
        var couponTitleName = localizer["CouponTitleField"];
        var discountTypeName = localizer["CouponDiscountTypeField"];
        var discountValueName = localizer["CouponDiscountValueField"];
        var endDateName = localizer["CouponEndDateField"];
        var usageLimitName = localizer["CouponUsageLimitField"];
        var perUserLimitName = localizer["CouponPerUserLimitField"];

        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .WithName(couponIdName);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"])
            .WithName(couponCodeName);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"])
            .WithName(couponTitleName);

        RuleFor(x => x.DiscountType)
            .NotEmpty()
            .WithMessage(x => localizer["RequiredField"])
            .IsEnumName(typeof(CouponDiscountType), caseSensitive: false)
            .WithMessage(x => localizer["InvalidEnum"])
            .WithName(discountTypeName);

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"])
            .WithName(discountValueName);

        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100)
            .When(x => x.DiscountType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
            .WithMessage(x => localizer["PercentageTooHigh"])
            .WithName(discountValueName);

        RuleFor(x => x.EndsAtUtc)
            .GreaterThan(x => x.StartsAtUtc)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(x => localizer["InvalidDateRange"])
            .WithName(endDateName);

        RuleFor(x => x.UsageLimit)
            .GreaterThan(0).When(x => x.UsageLimit.HasValue)
            .WithMessage(x => localizer["GreaterThanZero"])
            .WithName(usageLimitName);

        RuleFor(x => x.PerUserLimit)
            .GreaterThan(0).When(x => x.PerUserLimit.HasValue)
            .WithMessage(x => localizer["GreaterThanZero"])
            .WithName(perUserLimitName);
    }
}

public class UpdateCouponCommandHandler : IRequestHandler<UpdateCouponCommand, CouponAdminDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CouponAdminDto> Handle(UpdateCouponCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Coupons
            .Include(x => x.ApplicableVendors)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.Id);

        var code = request.Code.Trim().ToUpperInvariant();
        var duplicateExists = await _context.Coupons
            .AnyAsync(x => x.Id != request.Id && x.Code == code, cancellationToken);

        if (duplicateExists)
        {
            throw new BusinessRuleException("DUPLICATE_COUPON_CODE", "Coupon code already exists.");
        }

        var discountType = Enum.Parse<CouponDiscountType>(request.DiscountType, ignoreCase: true);
        entity.UpdateDetails(
            code,
            request.Title,
            discountType,
            request.DiscountValue,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.UsageLimit,
            request.PerUserLimit);
        entity.UpdateStatus(request.IsActive);

        var targetVendorIds = request.VendorIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet() ?? [];

        if (targetVendorIds.Count > 0)
        {
            var existingVendorIds = await _context.Vendors
                .Where(x => targetVendorIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var missingVendorId = targetVendorIds.FirstOrDefault(id => !existingVendorIds.Contains(id));
            if (missingVendorId != Guid.Empty)
            {
                throw new NotFoundException("Vendor", missingVendorId);
            }
        }

        var currentVendorIds = entity.ApplicableVendors.Select(x => x.VendorId).ToHashSet();
        var assignmentsToRemove = entity.ApplicableVendors
            .Where(x => !targetVendorIds.Contains(x.VendorId))
            .ToList();

        foreach (var assignment in assignmentsToRemove)
        {
            _context.CouponVendors.Remove(assignment);
        }

        foreach (var vendorId in targetVendorIds)
        {
            if (!currentVendorIds.Contains(vendorId))
            {
                _context.CouponVendors.Add(new CouponVendor(entity.Id, vendorId));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await MarketingCouponMappings.ToCouponDto(_context, entity.Id, cancellationToken);
    }
}

public class ActivateCouponCommandHandler : IRequestHandler<ActivateCouponCommand>
{
    private readonly IApplicationDbContext _context;

    public ActivateCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ActivateCouponCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Coupons.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.Id);
        entity.UpdateStatus(true);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeactivateCouponCommandHandler : IRequestHandler<DeactivateCouponCommand>
{
    private readonly IApplicationDbContext _context;

    public DeactivateCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeactivateCouponCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Coupons.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.Id);
        entity.UpdateStatus(false);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeleteCouponCommandHandler : IRequestHandler<DeleteCouponCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteCouponCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Coupons.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.Id);
        _context.Coupons.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

internal static class MarketingCouponMappings
{
    public static async Task<CouponAdminDto> ToCouponDto(
        IApplicationDbContext context,
        Guid couponId,
        CancellationToken cancellationToken)
    {
        var projection = await context.Coupons
            .AsNoTracking()
            .Where(x => x.Id == couponId)
            .Select(x => new CouponDetailsProjection(
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
            .FirstOrDefaultAsync(cancellationToken);

        if (projection is null)
        {
            throw new NotFoundException(nameof(Coupon), couponId);
        }

        var applicableVendors = await context.CouponVendors
            .AsNoTracking()
            .Where(x => x.CouponId == couponId)
            .OrderBy(x => x.Vendor.BusinessNameAr)
            .ThenBy(x => x.Vendor.BusinessNameEn)
            .Select(x => new CouponVendorAdminDto(
                x.VendorId,
                x.Vendor.BusinessNameAr,
                x.Vendor.BusinessNameEn))
            .ToListAsync(cancellationToken);

        return new CouponAdminDto(
            projection.Id,
            projection.Code,
            projection.Title,
            projection.DiscountType,
            projection.DiscountValue,
            projection.MinOrderAmount,
            projection.MaxDiscountAmount,
            projection.StartsAtUtc,
            projection.EndsAtUtc,
            projection.UsageLimit,
            projection.PerUserLimit,
            projection.IsActive,
            projection.AssignedVendorsCount,
            applicableVendors,
            projection.CreatedAtUtc,
            projection.UpdatedAtUtc);
    }
}

internal sealed record CouponDetailsProjection(
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
