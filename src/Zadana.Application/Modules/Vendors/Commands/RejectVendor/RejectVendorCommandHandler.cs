using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RejectVendor;

public class RejectVendorCommandHandler : IRequestHandler<RejectVendorCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RejectVendorCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(RejectVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
        var normalizedReason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new BusinessRuleException(
                "VendorRejectionReasonRequired",
                "A clear rejection reason is required.");
        }

        // Preserve the aggregate's status validation before evaluating correction targets.
        if (vendor.Status != VendorStatus.PendingReview)
        {
            vendor.Reject(normalizedReason);
            return;
        }

        var hasCorrectionTarget = vendor.DocumentReviews.Any(review =>
                VendorReviewWorkflow.IsRequired(review.Type)
                && review.Decision == VendorDocumentReviewDecision.Rejected)
            || vendor.ProfileReviewItems.Any(item => item.Status == VendorProfileReviewStatus.Rejected);

        if (!string.IsNullOrWhiteSpace(request.DocumentId))
        {
            var documentType = ParseRequiredDocumentType(request.DocumentId);
            VendorReviewWorkflow.EnsureDocumentCanBeReviewed(vendor, documentType);

            var review = vendor.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
            if (review is null)
            {
                review = new VendorDocumentReview(vendor.Id, documentType);
                vendor.DocumentReviews.Add(review);
            }

            review.Reject(normalizedReason, _currentUserService.UserId, "Vendor Compliance Desk");
            hasCorrectionTarget = true;
        }

        if (!hasCorrectionTarget)
        {
            throw new BusinessRuleException(
                "VendorReviewCorrectionTargetRequired",
                "Reject at least one required document or profile field before rejecting the vendor application.");
        }

        vendor.Reject(normalizedReason);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "rejected",
            "danger",
            normalizedReason,
            "مراجعة الامتثال",
            "مكتب امتثال التاجر",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_rejected",
                "رفضنا طلب اعتماد التاجر",
                "Vendor application rejected",
                normalizedReason,
                normalizedReason,
                "/profile",
                vendor.Id,
                SendPush: true),
            cancellationToken);
    }

    private static VendorDocumentType ParseRequiredDocumentType(string documentId)
    {
        if (!Enum.TryParse<VendorDocumentType>(documentId, true, out var documentType)
            || !VendorReviewWorkflow.IsRequired(documentType))
        {
            throw new BusinessRuleException(
                "VendorReviewInvalidCorrectionDocument",
                "A valid required vendor document must be selected before rejection.");
        }

        return documentType;
    }
}
