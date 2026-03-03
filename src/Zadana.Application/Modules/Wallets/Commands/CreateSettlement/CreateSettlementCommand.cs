using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Wallets.Commands.CreateSettlement;

public record CreateSettlementCommand(
    Guid? VendorId,
    Guid? DriverId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount) : MediatR.IRequest<Guid>;

public class CreateSettlementCommandValidator : AbstractValidator<CreateSettlementCommand>
{
    public CreateSettlementCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x)
            .Must(x => x.VendorId.HasValue || x.DriverId.HasValue)
            .WithMessage(x => localizer["EitherVendorOrDriverRequired"]);

        RuleFor(x => x.GrossAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.CommissionAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.NetAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);
    }
}
