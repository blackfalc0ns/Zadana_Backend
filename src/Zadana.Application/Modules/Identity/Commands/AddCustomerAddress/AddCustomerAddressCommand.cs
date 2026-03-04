using FluentValidation;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Commands.AddCustomerAddress;

public record AddCustomerAddressCommand(
    Guid UserId,
    string ContactName,
    string ContactPhone,
    string AddressLine,
    string? Label, // "Home", "Work", "Other"
    string? BuildingNo,
    string? FloorNo,
    string? ApartmentNo,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude) : MediatR.IRequest<Guid>;

public class AddCustomerAddressCommandValidator : AbstractValidator<AddCustomerAddressCommand>
{
    public AddCustomerAddressCommandValidator(Microsoft.Extensions.Localization.IStringLocalizer<Zadana.Application.Common.Localization.SharedResource> localizer)
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        
        RuleFor(x => x.ContactName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["ContactName"]);

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["ContactPhone"]);

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["AddressLine"]);

        RuleFor(x => x.Label)
            .IsEnumName(typeof(AddressLabel), caseSensitive: false).When(x => !string.IsNullOrEmpty(x.Label))
            .WithMessage(x => localizer["InvalidEnum"])
            .WithName(x => localizer["Label"]);
        RuleFor(x => x.BuildingNo).MaximumLength(50).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["BuildingNo"]);
        RuleFor(x => x.FloorNo).MaximumLength(50).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["FloorNo"]);
        RuleFor(x => x.ApartmentNo).MaximumLength(50).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["ApartmentNo"]);
        RuleFor(x => x.City).MaximumLength(100).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["City"]);
        RuleFor(x => x.Area).MaximumLength(100).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["Area"]);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithMessage(x => localizer["InvalidRange"])
            .WithName(x => localizer["Latitude"]);
            
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithMessage(x => localizer["InvalidRange"])
            .WithName(x => localizer["Longitude"]);
    }
}
