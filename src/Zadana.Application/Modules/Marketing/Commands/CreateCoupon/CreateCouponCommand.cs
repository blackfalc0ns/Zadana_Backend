using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Marketing.Enums;

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
    int? PerUserLimit) : MediatR.IRequest<Guid>;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.DiscountType)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .IsEnumName(typeof(CouponDiscountType), caseSensitive: false)
            .WithMessage(x => localizer["InvalidEnum"]);

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100).When(x => x.DiscountType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
            .WithMessage(x => localizer["PercentageTooHigh"]);

        RuleFor(x => x.EndsAtUtc)
            .GreaterThan(x => x.StartsAtUtc).When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(x => localizer["InvalidDateRange"]);

        RuleFor(x => x.UsageLimit)
            .GreaterThan(0).When(x => x.UsageLimit.HasValue)
            .WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.PerUserLimit)
            .GreaterThan(0).When(x => x.PerUserLimit.HasValue)
            .WithMessage(x => localizer["GreaterThanZero"]);
    }
}
