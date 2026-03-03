using FluentValidation;

namespace Zadana.Application.Modules.Delivery.Commands.AddCustomerAddress;

public record AddCustomerAddressCommand(
    Guid UserId,
    string ContactName,
    string ContactPhone,
    string AddressLine,
    string? Label,
    string? BuildingNo,
    string? FloorNo,
    string? ApartmentNo,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude) : MediatR.IRequest<Guid>;

public class AddCustomerAddressCommandValidator : AbstractValidator<AddCustomerAddressCommand>
{
    public AddCustomerAddressCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
        
        RuleFor(x => x.ContactName)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(200).WithMessage("Contact name cannot exceed 200 characters.");

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage("Contact phone is required.")
            .MaximumLength(50).WithMessage("Contact phone cannot exceed 50 characters.");

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("Address line is required.")
            .MaximumLength(500).WithMessage("Address line cannot exceed 500 characters.");

        RuleFor(x => x.Label).MaximumLength(100);
        RuleFor(x => x.BuildingNo).MaximumLength(50);
        RuleFor(x => x.FloorNo).MaximumLength(50);
        RuleFor(x => x.ApartmentNo).MaximumLength(50);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Area).MaximumLength(100);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithMessage("Invalid latitude.");
            
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithMessage("Invalid longitude.");
    }
}
