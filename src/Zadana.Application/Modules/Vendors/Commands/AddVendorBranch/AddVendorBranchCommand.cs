using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Support;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorBranch;

public record AddVendorBranchCommand(
    Guid VendorId,
    string Name,
    string AddressLine,
    string? Phone,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude,
    string? OpensAt,
    string? ClosesAt) : MediatR.IRequest<Guid>;

public class AddVendorBranchCommandValidator : AbstractValidator<AddVendorBranchCommand>
{
    public AddVendorBranchCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Area)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Latitude)
            .NotNull().WithMessage(x => localizer["RequiredField"])
            .InclusiveBetween(-90, 90).WithMessage(x => localizer["InvalidRange"]);

        RuleFor(x => x.Longitude)
            .NotNull().WithMessage(x => localizer["RequiredField"])
            .InclusiveBetween(-180, 180).WithMessage(x => localizer["InvalidRange"]);

        RuleFor(x => x)
            .Must(x => VendorBranchCoordinateValidation.AreMeaningful(x.Latitude, x.Longitude))
            .WithMessage(x => localizer["InvalidValue"])
            .OverridePropertyName(nameof(AddVendorBranchCommand.Latitude));

        RuleFor(x => x.OpensAt)
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.ClosesAt)
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"]);
    }
}
