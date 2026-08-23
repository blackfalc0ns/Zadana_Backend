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

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorContact;

public record UpdateVendorContactCommand(
    string Region,
    string City,
    string NationalAddress,
    decimal? BranchLatitude,
    decimal? BranchLongitude) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorContactCommandValidator : AbstractValidator<UpdateVendorContactCommand>
{
    public UpdateVendorContactCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NationalAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BranchLatitude).InclusiveBetween(-90, 90).When(x => x.BranchLatitude.HasValue);
        RuleFor(x => x.BranchLongitude).InclusiveBetween(-180, 180).When(x => x.BranchLongitude.HasValue);
        RuleFor(x => x)
            .Must(x => VendorBranchCoordinateValidation.AreBothMissingOrBothMeaningful(x.BranchLatitude, x.BranchLongitude))
            .WithMessage("Branch map coordinates are required.")
            .OverridePropertyName(nameof(UpdateVendorContactCommand.BranchLatitude));
    }
}

public class UpdateVendorContactCommandHandler : IRequestHandler<UpdateVendorContactCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IAdminAlertService _adminAlertService;
    private readonly IApplicationDbContext _context;

    public UpdateVendorContactCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        IAdminAlertService adminAlertService,
        IApplicationDbContext context)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _adminAlertService = adminAlertService;
        _context = context;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorContactCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

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

        VendorProfileReviewMutations.ResetSectionToSubmitted(vendor, "contact");
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-contact-updated",
            "info",
            "حدّثنا بيانات العنوان والموقع من بوابة التاجر.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        await VendorProfileSectionAdminAlerts.NotifySectionReviewAsync(
            _adminAlertService,
            vendor,
            "contact",
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
