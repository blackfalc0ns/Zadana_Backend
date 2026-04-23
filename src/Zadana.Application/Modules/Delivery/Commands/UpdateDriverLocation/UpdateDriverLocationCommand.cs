using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverLocation;

public record UpdateDriverLocationCommand(
    Guid DriverId,
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters) : MediatR.IRequest<Unit>;

public class UpdateDriverLocationCommandValidator : AbstractValidator<UpdateDriverLocationCommand>
{
    public UpdateDriverLocationCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.DriverId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage(x => localizer["InvalidRange"]);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage(x => localizer["InvalidRange"]);

        RuleFor(x => x.AccuracyMeters)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AccuracyMeters.HasValue)
            .WithMessage(x => localizer["InvalidRange"]);
    }
}
