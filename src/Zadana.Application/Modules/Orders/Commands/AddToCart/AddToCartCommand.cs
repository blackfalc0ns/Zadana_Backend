using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Orders.Commands.AddToCart;

public record AddToCartCommand(
    Guid UserId,
    Guid VendorId,
    Guid VendorProductId,
    int Quantity) : MediatR.IRequest<Guid>;

public class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.VendorProductId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
    }
}
