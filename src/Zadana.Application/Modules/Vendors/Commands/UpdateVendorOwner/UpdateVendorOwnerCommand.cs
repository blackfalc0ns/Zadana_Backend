using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorOwner;

public record UpdateVendorOwnerCommand(
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorOwnerCommandValidator : AbstractValidator<UpdateVendorOwnerCommand>
{
    public UpdateVendorOwnerCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.OwnerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.IdNumber).MaximumLength(50);
        RuleFor(x => x.Nationality).MaximumLength(100);
    }
}

public class UpdateVendorOwnerCommandHandler : IRequestHandler<UpdateVendorOwnerCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;
    private readonly IAdminAlertService _adminAlertService;

    public UpdateVendorOwnerCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        IProfileChangeApprovalService profileChangeApprovalService,
        IAdminAlertService adminAlertService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _profileChangeApprovalService = profileChangeApprovalService;
        _adminAlertService = adminAlertService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorOwnerCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var payload = new VendorOwnerProfileChangePayload(
            vendor.Id,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            request.IdNumber,
            request.Nationality);

        await _profileChangeApprovalService.SubmitAsync(
            userId,
            vendor.UserId,
            ProfileChangeApprovalActions.VendorProfileOwner,
            $"Vendor {vendor.BusinessNameEn} requested owner profile changes.",
            payload,
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.VendorCriticalChangeSubmitted,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "تعديل بيانات مالك بانتظار الموافقة",
                "Vendor owner change pending approval",
                $"أرسل التاجر {vendor.BusinessNameAr} تعديل بيانات المالك وينتظر موافقة الأدمن.",
                $"Vendor {vendor.BusinessNameEn} submitted owner profile changes pending admin approval.",
                vendor.Id,
                "/admin/access/approvals",
                new { vendorId = vendor.Id, userId = vendor.UserId, section = "owner" }),
            cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-owner-change-submitted",
            "info",
            "أرسل التاجر تعديل بيانات المالك للمراجعة قبل التطبيق.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        await VendorProfileSectionAdminAlerts.NotifySectionReviewAsync(
            _adminAlertService,
            vendor,
            "owner",
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
