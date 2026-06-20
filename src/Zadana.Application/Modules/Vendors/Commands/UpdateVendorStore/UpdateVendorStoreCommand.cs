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
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;
    private readonly IAdminAlertService _adminAlertService;

    public UpdateVendorStoreCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        IProfileChangeApprovalService profileChangeApprovalService,
        IAdminAlertService adminAlertService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _profileChangeApprovalService = profileChangeApprovalService;
        _adminAlertService = adminAlertService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorStoreCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var hasSensitiveChange =
            HasChanged(vendor.CommercialRegisterDocumentUrl, request.CommercialRegisterDocumentUrl) ||
            HasChanged(vendor.CommercialRegistrationNumber, request.CommercialRegistrationNumber);

        vendor.UpdateStore(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.LogoUrl,
            hasSensitiveChange ? null : request.CommercialRegisterDocumentUrl,
            request.Region,
            request.City,
            request.NationalAddress,
            hasSensitiveChange ? vendor.CommercialRegistrationNumber : request.CommercialRegistrationNumber);

        VendorProfileReviewMutations.ResetSectionToSubmitted(vendor, "store");
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (hasSensitiveChange)
        {
            var payload = new VendorStoreProfileChangePayload(
                vendor.Id,
                request.CommercialRegisterDocumentUrl,
                request.CommercialRegistrationNumber);

            await _profileChangeApprovalService.SubmitAsync(
                userId,
                vendor.UserId,
                ProfileChangeApprovalActions.VendorProfileStore,
                $"Vendor {vendor.BusinessNameEn} requested commercial registration changes.",
                payload,
                new ProfileChangeApprovalAlert(
                    AdminAlertTypes.VendorCriticalChangeSubmitted,
                    AdminAlertCategories.Vendors,
                    AdminAlertPriorities.High,
                    "تعديل سجل تجاري بانتظار الموافقة",
                    "Commercial registration change pending approval",
                    $"أرسل التاجر {vendor.BusinessNameAr} تعديل السجل التجاري وينتظر موافقة الأدمن.",
                    $"Vendor {vendor.BusinessNameEn} submitted commercial registration changes pending admin approval.",
                    vendor.Id,
                    "/admin/access/approvals",
                    new { vendorId = vendor.Id, userId = vendor.UserId, section = "store", sensitiveFields = new[] { "commercialRegistration" } }),
                cancellationToken);
        }

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            hasSensitiveChange ? "profile-store-updated-sensitive-submitted" : "profile-store-updated",
            "info",
            hasSensitiveChange
                ? "تم تطبيق بيانات المتجر العامة وإرسال بيانات السجل التجاري للمراجعة قبل التطبيق."
                : "تم تحديث بيانات المتجر من بوابة التاجر.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        await VendorProfileSectionAdminAlerts.NotifySectionReviewAsync(
            _adminAlertService,
            vendor,
            "store",
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }

    private static bool HasChanged(string? currentValue, string? nextValue) =>
        !string.IsNullOrWhiteSpace(nextValue) &&
        !string.Equals(currentValue?.Trim(), nextValue.Trim(), StringComparison.OrdinalIgnoreCase);
}
