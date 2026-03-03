using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Delivery.Commands.AssignDelivery;

public record AssignDeliveryCommand(
    Guid OrderId,
    Guid DriverId,
    decimal CodAmount) : MediatR.IRequest<Guid>;

public class AssignDeliveryCommandValidator : AbstractValidator<AssignDeliveryCommand>
{
    public AssignDeliveryCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.DriverId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        
        RuleFor(x => x.CodAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);
    }
}
