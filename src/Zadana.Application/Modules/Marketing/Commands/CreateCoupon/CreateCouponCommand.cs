using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Marketing.Commands.Coupons;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Commands.CreateCoupon;

public record CreateCouponCommand(
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
    IReadOnlyCollection<Guid>? VendorIds) : IRequest<CouponAdminDto>;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        var couponCodeName = localizer["CouponCodeField"];
        var couponTitleName = localizer["CouponTitleField"];
        var discountTypeName = localizer["CouponDiscountTypeField"];
        var discountValueName = localizer["CouponDiscountValueField"];
        var endDateName = localizer["CouponEndDateField"];
        var usageLimitName = localizer["CouponUsageLimitField"];
        var perUserLimitName = localizer["CouponPerUserLimitField"];

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"])
            .WithName(couponCodeName);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"])
            .WithName(couponTitleName);

        RuleFor(x => x.DiscountType)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .IsEnumName(typeof(CouponDiscountType), caseSensitive: false)
            .WithMessage(x => localizer["InvalidEnum"])
            .WithName(discountTypeName);

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"])
            .WithName(discountValueName);

        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100).When(x => x.DiscountType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
            .WithMessage(x => localizer["PercentageTooHigh"])
            .WithName(discountValueName);

        RuleFor(x => x.EndsAtUtc)
            .GreaterThan(x => x.StartsAtUtc).When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
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

public class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, CouponAdminDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CouponAdminDto> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Coupons.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("DUPLICATE_COUPON_CODE", "Coupon code already exists.");
        }

        var discountType = Enum.Parse<CouponDiscountType>(request.DiscountType, ignoreCase: true);

        var entity = new Coupon(
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

        _context.Coupons.Add(entity);

        var vendorIds = request.VendorIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (vendorIds.Length > 0)
        {
            var existingVendorIds = await _context.Vendors
                .Where(x => vendorIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var missingVendorId = vendorIds.FirstOrDefault(id => !existingVendorIds.Contains(id));
            if (missingVendorId != Guid.Empty)
            {
                throw new NotFoundException("Vendor", missingVendorId);
            }

            foreach (var vendorId in existingVendorIds)
            {
                _context.CouponVendors.Add(new CouponVendor(entity.Id, vendorId));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await MarketingCouponMappings.ToCouponDto(_context, entity.Id, cancellationToken);
    }
}
