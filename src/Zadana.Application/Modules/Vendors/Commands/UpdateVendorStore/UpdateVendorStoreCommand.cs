using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorStore;

public record UpdateVendorStoreCommand(
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
    string? CommercialRegistrationNumber) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorStoreCommandValidator : AbstractValidator<UpdateVendorStoreCommand>
{
    public UpdateVendorStoreCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.BusinessNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DescriptionAr).MaximumLength(2000);
        RuleFor(x => x.DescriptionEn).MaximumLength(2000);
        RuleFor(x => x.Region).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.NationalAddress).MaximumLength(500);
        RuleFor(x => x.CommercialRegistrationNumber).MaximumLength(50);
    }
}

public class UpdateVendorStoreCommandHandler : IRequestHandler<UpdateVendorStoreCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IAdminAlertService _adminAlertService;

    public UpdateVendorStoreCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        IAdminAlertService adminAlertService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _adminAlertService = adminAlertService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorStoreCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

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

        VendorProfileReviewMutations.ResetSectionToSubmitted(vendor, "store");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-store-updated",
            "info",
            "قام التاجر بتحديث بيانات المتجر من بوابة التاجر.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.VendorStoreUpdated,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "تعديل بيانات المتجر",
                "Vendor store details updated",
                $"قام التاجر {vendor.BusinessNameAr} بتعديل بيانات المتجر.",
                $"Vendor {vendor.BusinessNameEn} updated store profile details.",
                vendor.Id,
                $"/vendors/{vendor.Id}",
                new { vendorId = vendor.Id, userId = vendor.UserId, section = "store" }),
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
