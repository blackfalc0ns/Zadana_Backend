using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.SubmitVendorReview;

public record SubmitVendorReviewCommand : IRequest<VendorWorkspaceDto>;

public sealed class SubmitVendorReviewCommandHandler : IRequestHandler<SubmitVendorReviewCommand, VendorWorkspaceDto>
{
    private static readonly VendorDocumentType[] RequiredDocumentTypes =
    [
        VendorDocumentType.Commercial,
        VendorDocumentType.Tax,
        VendorDocumentType.License
    ];

    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminAlertService _adminAlertService;

    public SubmitVendorReviewCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorReviewAuditService vendorReviewAuditService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAdminAlertService adminAlertService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _adminAlertService = adminAlertService;
    }

    public async Task<VendorWorkspaceDto> Handle(SubmitVendorReviewCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        VendorReviewWorkflow.EnsureComplianceActionAllowed(vendor);

        var missingRequired = RequiredDocumentTypes
            .Where(type => !VendorReviewWorkflow.IsUploaded(vendor, type))
            .Select(type => type.ToString())
            .ToList();

        if (missingRequired.Count > 0)
        {
            throw new BusinessRuleException(
                "VendorReviewRequiredDocumentsMissing",
                $"Required vendor documents are missing: {string.Join(", ", missingRequired)}.");
        }

        var rejectedRequired = vendor.DocumentReviews
            .Where(item => RequiredDocumentTypes.Contains(item.Type) && item.Decision == VendorDocumentReviewDecision.Rejected)
            .Select(item => item.Type.ToString())
            .ToList();

        if (rejectedRequired.Count > 0)
        {
            throw new BusinessRuleException(
                "VendorReviewRejectedDocumentsMustBeReuploaded",
                $"Rejected vendor documents must be re-uploaded first: {string.Join(", ", rejectedRequired)}.");
        }

        var missingRequiredFields = VendorProfileReviewCatalog.Definitions
            .Where(item => item.TargetType == VendorProfileReviewTargetType.Field && item.IsRequired)
            .Where(item => string.IsNullOrWhiteSpace(item.ValueAccessor(vendor)))
            .Select(item => item.Code)
            .ToList();

        if (missingRequiredFields.Count > 0)
        {
            throw new BusinessRuleException(
                "VendorReviewRequiredFieldsMissing",
                $"Required vendor fields are missing: {string.Join(", ", missingRequiredFields)}.");
        }

        var rejectedFields = vendor.ProfileReviewItems
            .Where(item => item.Status == VendorProfileReviewStatus.Rejected)
            .Select(item => item.Code)
            .ToList();

        if (rejectedFields.Count > 0)
        {
            throw new BusinessRuleException(
                "VendorReviewRejectedFieldsMustBeUpdated",
                $"Rejected vendor fields must be updated first: {string.Join(", ", rejectedFields)}.");
        }

        if (vendor.Status == VendorStatus.Rejected)
        {
            vendor.ReopenForReview();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "vendor-profile-submitted",
            "info",
            "قام التاجر بإرسال الملف الشخصي والمستندات المطلوبة لمراجعة الامتثال.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.VendorDocumentsSubmitted,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "مستندات تاجر جاهزة للمراجعة",
                "Vendor documents ready for review",
                $"قام التاجر {vendor.BusinessNameAr} بإرسال بياناته ومستنداته للمراجعة.",
                $"Vendor {vendor.BusinessNameEn} submitted profile and documents for review.",
                vendor.Id,
                $"/vendors/{vendor.Id}/compliance",
                new
                {
                    vendorId = vendor.Id,
                    userId = vendor.UserId,
                    status = vendor.Status.ToString(),
                    section = "legal"
                }),
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
