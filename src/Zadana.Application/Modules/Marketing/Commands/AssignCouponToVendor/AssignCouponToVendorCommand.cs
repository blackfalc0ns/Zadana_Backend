using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Marketing.Commands.AssignCouponToVendor;

public record AssignCouponToVendorCommand(
    Guid CouponId,
    Guid VendorId) : MediatR.IRequest<Guid>;

public class AssignCouponToVendorCommandValidator : AbstractValidator<AssignCouponToVendorCommand>
{
    public AssignCouponToVendorCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CouponId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}
