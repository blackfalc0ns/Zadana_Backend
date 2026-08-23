using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorContact;

public record AdminUpdateVendorContactCommand(
    Guid VendorId,
    string Region,
    string City,
    string NationalAddress,
    decimal? BranchLatitude,
    decimal? BranchLongitude) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorContactCommandValidator : AbstractValidator<AdminUpdateVendorContactCommand>
{
    public AdminUpdateVendorContactCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NationalAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BranchLatitude).InclusiveBetween(-90, 90).When(x => x.BranchLatitude.HasValue);
        RuleFor(x => x.BranchLongitude).InclusiveBetween(-180, 180).When(x => x.BranchLongitude.HasValue);
        RuleFor(x => x)
            .Must(x => VendorBranchCoordinateValidation.AreBothMissingOrBothMeaningful(x.BranchLatitude, x.BranchLongitude))
            .WithMessage("Branch map coordinates are required.")
            .OverridePropertyName(nameof(AdminUpdateVendorContactCommand.BranchLatitude));
    }
}

public class AdminUpdateVendorContactCommandHandler : IRequestHandler<AdminUpdateVendorContactCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public AdminUpdateVendorContactCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        IApplicationDbContext context)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorContactCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        await OperationalGeographyScope.EnsureOperationalRegionCityAsync(
            _context,
            request.Region,
            request.City,
            cancellationToken);

        vendor.UpdateContact(request.Region, request.City, request.NationalAddress);

        if (request.BranchLatitude.HasValue && request.BranchLongitude.HasValue)
        {
            vendor.SetStoreLocation(request.BranchLatitude.Value, request.BranchLongitude.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_contact_updated",
                "حدّثنا عنوان المتجر",
                "Vendor contact details updated",
                "حدّثنا بيانات العنوان والتواصل من لوحة الإدارة.",
                "Your contact and address details were updated by the admin team.",
                "/profile",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
