using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorStore;

public record AdminUpdateVendorStoreCommand(
    Guid VendorId,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    string? Region,
    string? City,
    string? NationalAddress,
    string? CommercialRegistrationNumber,
    decimal? BranchLatitude = null,
    decimal? BranchLongitude = null) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorStoreCommandValidator : AbstractValidator<AdminUpdateVendorStoreCommand>
{
    public AdminUpdateVendorStoreCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.BusinessNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Region).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.NationalAddress).MaximumLength(500);
        RuleFor(x => x.CommercialRegistrationNumber).MaximumLength(50);
        RuleFor(x => x.BranchLatitude).InclusiveBetween(-90, 90).When(x => x.BranchLatitude.HasValue);
        RuleFor(x => x.BranchLongitude).InclusiveBetween(-180, 180).When(x => x.BranchLongitude.HasValue);
        RuleFor(x => x)
            .Must(x => VendorBranchCoordinateValidation.AreBothMissingOrBothMeaningful(x.BranchLatitude, x.BranchLongitude))
            .WithMessage("Branch map coordinates are required.")
            .OverridePropertyName(nameof(AdminUpdateVendorStoreCommand.BranchLatitude));
    }
}

public class AdminUpdateVendorStoreCommandHandler : IRequestHandler<AdminUpdateVendorStoreCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public AdminUpdateVendorStoreCommandHandler(
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

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorStoreCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        await OperationalGeographyScope.EnsureOperationalRegionCityIfProvidedAsync(
            _context,
            request.Region,
            request.City,
            cancellationToken);

        vendor.UpdateStore(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl,
            request.Region,
            request.City,
            request.NationalAddress,
            request.CommercialRegistrationNumber);

        if (request.BranchLatitude.HasValue && request.BranchLongitude.HasValue)
        {
            vendor.SetStoreLocation(request.BranchLatitude.Value, request.BranchLongitude.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_store_updated",
                "حدّثنا بيانات المتجر",
                "Vendor store details updated",
                "حدّثنا بيانات المتجر الأساسية من لوحة الإدارة.",
                "Your store profile details were updated by the admin team.",
                "/profile",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
