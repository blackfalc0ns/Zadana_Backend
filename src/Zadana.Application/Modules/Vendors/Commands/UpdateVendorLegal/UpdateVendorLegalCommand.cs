using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorLegal;

public record UpdateVendorLegalCommand(
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string? TaxId,
    string? LicenseNumber,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorLegalCommandValidator : AbstractValidator<UpdateVendorLegalCommand>
{
    public UpdateVendorLegalCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.LicenseNumber).MaximumLength(100);
    }
}

public class UpdateVendorLegalCommandHandler : IRequestHandler<UpdateVendorLegalCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;

    public UpdateVendorLegalCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        IProfileChangeApprovalService profileChangeApprovalService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _profileChangeApprovalService = profileChangeApprovalService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorLegalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var payload = new VendorLegalProfileChangePayload(
            vendor.Id,
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.CommercialRegisterDocumentUrl,
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl);

        await _profileChangeApprovalService.SubmitAsync(
            userId,
            vendor.UserId,
            ProfileChangeApprovalActions.VendorProfileLegal,
            $"Vendor {vendor.BusinessNameEn} requested legal profile changes.",
            payload,
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.VendorCriticalChangeSubmitted,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "تعديل بيانات أو مستندات قانونية بانتظار الموافقة",
                "Vendor legal change pending approval",
                $"أرسل التاجر {vendor.BusinessNameAr} تعديل بيانات أو مستندات قانونية وينتظر موافقة الأدمن.",
                $"Vendor {vendor.BusinessNameEn} submitted legal or document changes pending admin approval.",
                vendor.Id,
                "/admin/access/approvals",
                new { vendorId = vendor.Id, userId = vendor.UserId, section = "legal" }),
            cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-legal-change-submitted",
            "warning",
            "أرسل التاجر تعديل البيانات أو المستندات القانونية للمراجعة قبل التطبيق.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
